using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Utils;

namespace ShoppingList;

[Injectable(InjectionType.Singleton)]
public sealed class Planner(
    ProfileHelper profileHelper,
    HideoutTable hideoutTable,
    TemplateTable templateTable,
    JsonUtil jsonUtil,
    TradersTable tradersTable,
    GlobalTable globalTable,
    TraderAssortHelper traderAssortHelper,
    TraderPurchasePersisterService purchasePersister,
    RagfairOfferService ragfairOfferService,
    HandbookHelper handbookHelper,
    PaymentHelper paymentHelper,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    HideoutInProgressReader hideoutInProgress,
    ISptLogger<Planner> logger)
{
    private readonly Dictionary<MongoId, int> _rarity = new();

    private sealed record Offer(string Source, MongoId Trader, MongoId Id, MongoId Currency, double UnitPrice, double UnitRub, int Available, bool Fir);

    public PlanResponse Plan(MongoId sessionId, PlanRequest request)
    {
        var pmc = profileHelper.GetPmcProfile(sessionId) ?? throw new InvalidOperationException("No PMC profile");
        var response = new PlanResponse { FleaUnlocked = pmc.Info?.Level >= globalTable.Configuration.RagFair.MinUserLevel };

        var (firHave, anyHave) = CountStash(pmc);
        var firPool = new Dictionary<MongoId, int>(firHave);
        var nonFirPool = anyHave.ToDictionary(kv => kv.Key, kv => kv.Value - firHave.GetValueOrDefault(kv.Key));

        var donations = hideoutInProgress.GetDonations(sessionId);
        var areaNeeds = new List<(AreaPlan plan, StageRequirement req, Need need)>();
        if (request.Overview)
        {
            foreach (var (area, stage, level, ready) in SelectAreas(pmc, null, all: true))
            {
                var preview = new AreaPlan { Type = (int)area.Type!.Value, Level = level, Ready = ready };
                // Each unticked area previews the stash independently; it reserves no items or purchases.
                var previewFir = new Dictionary<MongoId, int>(firHave);
                var previewNonFir = anyHave.ToDictionary(kv => kv.Key, kv => kv.Value - firHave.GetValueOrDefault(kv.Key));
                var donated = donations.GetValueOrDefault(preview.Type);
                foreach (var req in (stage.Requirements ?? []).Where(r => r.Type == "Item").OrderByDescending(r => r.IsSpawnedInSession == true))
                {
                    int required = req.Count ?? 0;
                    int given = Math.Min(required, donated?.GetValueOrDefault(req.TemplateId.ToString()) ?? 0);
                    int remaining = required - given;
                    bool fir = req.IsSpawnedInSession == true;
                    int missing = fir ? Take(previewFir, req.TemplateId, remaining) : Take(previewNonFir, req.TemplateId, Take(previewFir, req.TemplateId, remaining));
                    preview.Items.Add(new Need { Tpl = req.TemplateId.ToString(), Required = required, Donated = given,
                        Fir = fir, Have = remaining - missing, Missing = missing, Rarity = Rarity(req.TemplateId),
                        Plan = missing == 0 ? "ok" : paymentHelper.IsMoneyTpl(req.TemplateId) ? "money" : "later" });
                }
                response.AvailableAreas.Add(preview);
            }
        }

        foreach (var (area, stage, level, ready) in SelectAreas(pmc, request.ExcludeAll ? [-1] : request.Areas))
        {
            var plan = new AreaPlan { Type = (int)area.Type!.Value, Level = level, Ready = ready };
            response.Areas.Add(plan);
            var donated = donations.GetValueOrDefault(plan.Type);
            foreach (var req in (stage.Requirements ?? []).Where(r => r.Type == "Item"))
            {
                int required = req.Count ?? 0;
                var need = new Need
                {
                    Tpl = req.TemplateId.ToString(),
                    Required = required,
                    Donated = Math.Min(required, donated?.GetValueOrDefault(req.TemplateId.ToString()) ?? 0),
                    Fir = req.IsSpawnedInSession == true,
                };
                plan.Items.Add(need);
                areaNeeds.Add((plan, req, need));
            }
        }

        // FIR requirements first so non-FIR ones only use what is left over
        var toBuy = new Dictionary<MongoId, (int any, int fir)>();
        var firToCraft = new Dictionary<MongoId, int>();
        foreach (var (_, req, need) in areaNeeds.OrderByDescending(n => n.need.Fir))
        {
            var tpl = req.TemplateId;
            int stillNeeded = need.Required - need.Donated;
            int missing = need.Fir ? Take(firPool, tpl, stillNeeded) : Take(nonFirPool, tpl, Take(firPool, tpl, stillNeeded));
            need.Missing = missing;
            need.Have = stillNeeded - missing;
            need.Rarity = Rarity(tpl);
            if (missing == 0)
            {
                need.Plan = "ok";
            }
            else if (paymentHelper.IsMoneyTpl(tpl))
            {
                need.Plan = "money";
            }
            else if (!need.Fir)
            {
                need.Plan = "buy";
                Add(toBuy, tpl, missing, fir: false);
            }
            else if (traderConfig.PurchasesAreFoundInRaid || ragfairConfig.Dynamic.PurchasesAreFoundInRaid)
            {
                need.Plan = "buy";
                Add(toBuy, tpl, missing, fir: true);
            }
            else if (request.Crafts)
            {
                firToCraft[tpl] = firToCraft.GetValueOrDefault(tpl) + missing;
            }
        }

        var offers = new Dictionary<MongoId, List<Offer>>();
        List<Offer> OffersFor(MongoId tpl) => offers.TryGetValue(tpl, out var list) ? list : offers[tpl] = request.Raid ? [] : FindOffers(sessionId, pmc, tpl, response.FleaUnlocked);

        foreach (var (tpl, missing) in firToCraft)
        {
            var craft = PlanCraft(pmc, tpl, missing, firPool, nonFirPool, OffersFor);
            foreach (var need in areaNeeds.Select(n => n.need).Where(n => n.Fir && n.Tpl == tpl.ToString() && n.Missing > 0))
            {
                need.Plan = craft is null ? "none" : "craft";
            }

            if (craft is null)
            {
                continue;
            }

            response.Crafts.Add(craft);
            foreach (var ing in craft.Ingredients.Where(i => i.Missing > 0))
            {
                Add(toBuy, new MongoId(ing.Tpl), ing.Missing, fir: false);
            }
        }

        if (request.Future)
        {
            PlanFuture(pmc, request, response, firPool, nonFirPool);
        }

        if (!request.Raid) BuildPurchases(response, toBuy, OffersFor, areaNeeds.Select(n => n.need).Concat(response.Crafts.SelectMany(c => c.Ingredients)));
        foreach (var money in Money.GetMoneyTpls())
        {
            response.Money[money.ToString()] = anyHave.GetValueOrDefault(money);
        }

        return response;
    }

    // Later levels only show what the stash covers after the current plan, nothing is bought for them
    private void PlanFuture(PmcData pmc, PlanRequest request, PlanResponse response, Dictionary<MongoId, int> firPool, Dictionary<MongoId, int> nonFirPool)
    {
        var current = response.Areas.ToDictionary(a => a.Type, a => a.Level);
        var levels = new List<(HideoutArea area, int level)>();
        foreach (var area in hideoutTable.Areas)
        {
            if (area.Type is null || area.IsEnabled == false || area.Stages is null)
            {
                continue;
            }

            int type = (int)area.Type.Value;
            var profileArea = pmc.Hideout?.Areas?.FirstOrDefault(a => a.Type == area.Type);
            bool constructing = profileArea?.Constructing == true;
            int built = (profileArea?.Level ?? 0) + (constructing ? 1 : 0);
            bool wanted = request.Areas?.Contains(type) == true;
            int from;
            if (current.TryGetValue(type, out var next))
            {
                from = next + 1;
            }
            else if (constructing ? request.Overview || wanted : request.Overview && !wanted && !IsReady(pmc, area, built + 1))
            {
                // Unticked areas stay out; the overview adds areas still locked behind other upgrades
                from = built + (constructing ? 1 : 2);
            }
            else
            {
                continue;
            }

            for (int level = from; area.Stages.ContainsKey(level.ToString()); level++)
            {
                if (IsManualStage(area.Stages[level.ToString()])) levels.Add((area, level));
            }
        }

        foreach (var (area, level) in levels.OrderBy(l => l.level))
        {
            var plan = new AreaPlan { Type = (int)area.Type!.Value, Level = level };
            foreach (var req in (area.Stages![level.ToString()].Requirements ?? []).Where(r => r.Type == "Item"))
            {
                var tpl = req.TemplateId;
                var need = new Need { Tpl = tpl.ToString(), Required = req.Count ?? 0, Fir = req.IsSpawnedInSession == true, Rarity = Rarity(tpl) };
                need.Missing = need.Fir ? Take(firPool, tpl, need.Required) : Take(nonFirPool, tpl, Take(firPool, tpl, need.Required));
                need.Have = need.Required - need.Missing;
                need.Plan = need.Missing == 0 ? "ok" : paymentHelper.IsMoneyTpl(tpl) ? "money" : need.Fir ? "none" : "later";
                plan.Items.Add(need);
            }

            response.Future.Add(plan);
        }

        response.Future.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : a.Level.CompareTo(b.Level));
    }

    private static bool IsManualStage(Stage stage) => stage.AutoUpgrade != true;

    private static bool IsReady(PmcData pmc, HideoutArea area, int level) =>
        area.Stages?.GetValueOrDefault(level.ToString()) is { } stage && (stage.Requirements ?? []).All(r => r.Type switch
        {
            "Area" => (pmc.Hideout?.Areas?.FirstOrDefault(a => (int)a.Type == r.AreaType)?.Level ?? 0) >= (r.RequiredLevel ?? 0),
            "TraderLoyalty" => (pmc.TradersInfo?.GetValueOrDefault(r.TraderId)?.LoyaltyLevel ?? 0) >= (r.LoyaltyLevel ?? 0),
            _ => true,
        });

    // RarityPvE has no typed property, so it is read back from the serialized props
    private int Rarity(MongoId tpl)
    {
        if (_rarity.TryGetValue(tpl, out var known))
        {
            return known;
        }

        int rarity = -1;
        try
        {
            if (templateTable.Items.GetValueOrDefault(tpl)?.Properties is { } props)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(jsonUtil.Serialize(props, props.GetType())!);
                if (doc.RootElement.TryGetProperty("RarityPvE", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    rarity = value.GetString() switch { "Common" => 0, "Rare" => 1, "Superrare" => 2, _ => -1 };
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"[ShoppingList] No rarity for {tpl}: {ex.Message}");
        }

        return _rarity[tpl] = rarity;
    }

    private IEnumerable<(HideoutArea area, Stage stage, int level, bool ready)> SelectAreas(PmcData pmc, List<int>? wanted, bool all = false)
    {
        foreach (var area in hideoutTable.Areas)
        {
            if (area.Type is null || area.IsEnabled == false)
            {
                continue;
            }

            var profileArea = pmc.Hideout?.Areas?.FirstOrDefault(a => a.Type == area.Type);
            int next = (profileArea?.Level ?? 0) + 1;
            if (area.Stages?.GetValueOrDefault(next.ToString()) is not { } stage || profileArea?.Constructing == true || !IsManualStage(stage))
            {
                continue;
            }

            bool ready = IsReady(pmc, area, next);

            bool selected = all || (wanted is { Count: > 0 } ? wanted.Contains((int)area.Type.Value) : ready);
            if (selected)
            {
                yield return (area, stage, next, ready);
            }
        }
    }

    private CraftPlan? PlanCraft(PmcData pmc, MongoId tpl, int missing, Dictionary<MongoId, int> firPool, Dictionary<MongoId, int> nonFirPool, Func<MongoId, List<Offer>> offersFor)
    {
        var unlocked = pmc.UnlockedInfo?.UnlockedProductionRecipe ?? [];
        var candidates = (hideoutTable.Production.Recipes ?? [])
            .Where(r => r.AreaType is not null && (pmc.Hideout?.Areas?.FirstOrDefault(a => (int)a.Type == (int)r.AreaType.Value)?.Level ?? 0) > 0)
            .Where(r => r.EndProduct == tpl && r.Continuous != true && r.IsCodeProduction != true && (r.Locked != true || unlocked.Contains(r.Id)))
            .Where(r => (r.Requirements ?? []).Where(q => q.Type == "Area").All(q =>
                (pmc.Hideout?.Areas?.FirstOrDefault(a => (int)a.Type == q.AreaType)?.Level ?? 0) >= (q.RequiredLevel ?? 0)))
            .ToList();

        CraftPlan? best = null;
        double bestCost = double.MaxValue;
        foreach (var recipe in candidates)
        {
            int runs = (int)Math.Ceiling(missing / (double)Math.Max(1, recipe.Count ?? 1));
            var fir = new Dictionary<MongoId, int>(firPool);
            var nonFir = new Dictionary<MongoId, int>(nonFirPool);
            var ingredients = new List<Need>();
            double cost = 0;
            foreach (var req in (recipe.Requirements ?? []).Where(q => q.Type is "Item" or "Tool" && q.TemplateId is not null))
            {
                var ingTpl = req.TemplateId!.Value;
                bool tool = req.Type == "Tool";
                int required = tool ? 1 : (req.Count ?? 1) * runs;
                int have = nonFir.GetValueOrDefault(ingTpl) + fir.GetValueOrDefault(ingTpl);
                int left = Take(nonFir, ingTpl, Take(fir, ingTpl, required));
                var need = new Need { Tpl = ingTpl.ToString(), Required = required, Have = Math.Min(have, required), Missing = left, Tool = tool, Plan = left > 0 ? "buy" : "ok" };
                ingredients.Add(need);
                if (left > 0)
                {
                    var cheapest = offersFor(ingTpl).Select(o => o.UnitRub).DefaultIfEmpty(double.MaxValue / 1e6).Min();
                    cost += cheapest * left;
                }
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                best = new CraftPlan { Tpl = tpl.ToString(), Missing = missing, Recipe = recipe.Id.ToString(), Area = (int)(recipe.AreaType ?? 0), Runs = runs, Ingredients = ingredients };
            }
        }

        if (best is not null)
        {
            // Commit the stash items the chosen recipe uses so later recipes don't count them twice
            foreach (var ing in best.Ingredients)
            {
                var ingTpl = new MongoId(ing.Tpl);
                Take(nonFirPool, ingTpl, Take(firPool, ingTpl, ing.Required));
            }
        }

        return best;
    }

    private void BuildPurchases(PlanResponse response, Dictionary<MongoId, (int any, int fir)> toBuy, Func<MongoId, List<Offer>> offersFor, IEnumerable<Need> needs)
    {
        // tpl -> whether part of it can still be bought
        var unavailable = new Dictionary<string, bool>();
        foreach (var (tpl, (anyCount, firCount)) in toBuy)
        {
            var remaining = new Dictionary<bool, int> { [true] = firCount, [false] = anyCount };
            foreach (var offer in offersFor(tpl).OrderBy(o => o.Source == "trader" ? 0 : 1).ThenBy(o => o.UnitRub))
            {
                // A FIR offer can fill both kinds of need, a plain one only the non-FIR part
                int want = offer.Fir ? remaining[true] + remaining[false] : remaining[false];
                int count = Math.Min(want, offer.Available);
                if (count <= 0)
                {
                    continue;
                }

                int fromFir = offer.Fir ? Math.Min(count, remaining[true]) : 0;
                remaining[true] -= fromFir;
                remaining[false] -= count - fromFir;

                long price = (long)Math.Ceiling(offer.UnitPrice * count);
                long rub = (long)Math.Ceiling(offer.UnitRub * count);
                response.Purchases.Add(new Purchase { Tpl = tpl.ToString(), Count = count, Source = offer.Source, Trader = offer.Trader.ToString(), Currency = offer.Currency.ToString(), Price = price, Rub = rub });
                response.Cost[offer.Currency.ToString()] = response.Cost.GetValueOrDefault(offer.Currency.ToString()) + price;
                response.TotalRub += rub;

                var payment = new List<Dictionary<string, object>> { new() { ["id"] = offer.Currency.ToString(), ["count"] = price } };
                if (offer.Source == "trader")
                {
                    response.Operations.Add(new()
                    {
                        ["Action"] = "TradingConfirm",
                        ["type"] = "buy_from_trader",
                        ["tid"] = offer.Trader.ToString(),
                        ["item_id"] = offer.Id.ToString(),
                        ["count"] = count,
                        ["scheme_id"] = 0,
                        ["scheme_items"] = payment,
                    });
                }
                else
                {
                    // One offer per request: a single sold-out offer (BetterFlea snipes some at purchase) would fail a whole cart
                    response.Operations.Add(new()
                    {
                        ["Action"] = "RagFairBuyOffer",
                        ["offers"] = new List<Dictionary<string, object>> { new() { ["id"] = offer.Id.ToString(), ["count"] = count, ["items"] = payment } },
                    });
                }
            }

            int left = remaining[true] + remaining[false];
            if (left > 0)
            {
                response.Unavailable.Add(new Need { Tpl = tpl.ToString(), Required = anyCount + firCount, Missing = left, Plan = "unavailable" });
                unavailable[tpl.ToString()] = left < anyCount + firCount;
            }
        }

        foreach (var need in needs.Where(n => n.Plan == "buy" && unavailable.ContainsKey(n.Tpl)))
        {
            need.Plan = unavailable[need.Tpl] ? "partial" : "unavailable";
        }
    }

    private List<Offer> FindOffers(MongoId sessionId, PmcData pmc, MongoId tpl, bool fleaUnlocked)
    {
        var result = new List<Offer>();
        foreach (var traderId in tradersTable.Keys)
        {
            if (traderId == Traders.FENCE || pmc.TradersInfo?.GetValueOrDefault(traderId) is not { Unlocked: true } info)
            {
                continue;
            }

            try
            {
                var assort = traderAssortHelper.GetAssort(sessionId, traderId);
                foreach (var item in assort.Items.Where(i => i.Template == tpl && i.ParentId == "hideout"))
                {
                    if (assort.Items.Any(i => i.ParentId == item.Id.ToString())
                        || assort.LoyalLevelItems.GetValueOrDefault(item.Id, 1) > (info.LoyaltyLevel ?? 1)
                        || assort.BarterScheme.GetValueOrDefault(item.Id)?.FirstOrDefault() is not [{ } pay] || !paymentHelper.IsMoneyTpl(pay.Template))
                    {
                        continue;
                    }

                    int available = item.Upd?.UnlimitedCount == true ? 999999 : (int)(item.Upd?.StackObjectsCount ?? 0);
                    if (item.Upd?.BuyRestrictionMax is { } max)
                    {
                        int bought = (int)(purchasePersister.GetProfileTraderPurchase(sessionId, traderId, item.Id.ToString())?.PurchaseCount ?? 0);
                        available = Math.Min(available, (int)max - bought);
                    }

                    double unit = pay.Count ?? 0;
                    if (available > 0 && unit > 0)
                    {
                        result.Add(new Offer("trader", traderId, item.Id, pay.Template, unit, handbookHelper.InRoubles(unit, pay.Template), available, traderConfig.PurchasesAreFoundInRaid));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"[ShoppingList] Skipped trader {traderId}: {ex.Message}");
            }
        }

        if (!fleaUnlocked)
        {
            return result;
        }

        foreach (var offer in ragfairOfferService.GetOffers())
        {
            if (offer.Items is not [{ } item] || item.Template != tpl || offer.SellInOnePiece == true || offer.Locked == true
                || offer.User.Id == pmc.Id || offer.Quantity <= 0
                || offer.Requirements?.ToList() is not [{ } pay] || !paymentHelper.IsMoneyTpl(pay.TemplateId))
            {
                continue;
            }

            if (offer.IsTraderOffer() && (pmc.TradersInfo?.GetValueOrDefault(offer.User.Id)?.LoyaltyLevel ?? 0) < (offer.LoyaltyLevel ?? 1))
            {
                continue;
            }

            int available = offer.Quantity;
            if (offer.BuyRestrictionMax is { } max)
            {
                available = Math.Min(available, max - (offer.BuyRestrictionCurrent ?? 0));
            }

            double unit = pay.Count ?? 0;
            if (available > 0 && unit > 0)
            {
                bool fir = offer.IsTraderOffer() ? traderConfig.PurchasesAreFoundInRaid : ragfairConfig.Dynamic.PurchasesAreFoundInRaid;
                result.Add(new Offer("flea", offer.User.Id, offer.Id, pay.TemplateId, unit, handbookHelper.InRoubles(unit, pay.TemplateId), available, fir));
            }
        }

        return result;
    }

    private static (Dictionary<MongoId, int> fir, Dictionary<MongoId, int> any) CountStash(PmcData pmc)
    {
        var items = pmc.Inventory?.Items ?? [];
        var stashId = pmc.Inventory?.Stash?.ToString();
        var children = items.Where(i => i.ParentId is not null).ToLookup(i => i.ParentId!);
        var fir = new Dictionary<MongoId, int>();
        var any = new Dictionary<MongoId, int>();

        var stack = new Stack<string>();
        if (stashId is not null)
        {
            stack.Push(stashId);
        }

        while (stack.Count > 0)
        {
            foreach (var item in children[stack.Pop()])
            {
                int count = (int)(item.Upd?.StackObjectsCount ?? 1);
                any[item.Template] = any.GetValueOrDefault(item.Template) + count;
                if (item.Upd?.SpawnedInSession == true)
                {
                    fir[item.Template] = fir.GetValueOrDefault(item.Template) + count;
                }

                stack.Push(item.Id.ToString());
            }
        }

        return (fir, any);
    }

    // Takes up to `count` from the pool and returns what is still missing
    private static int Take(Dictionary<MongoId, int> pool, MongoId tpl, int count)
    {
        int have = pool.GetValueOrDefault(tpl);
        int used = Math.Min(have, count);
        pool[tpl] = have - used;
        return count - used;
    }

    private static void Add(Dictionary<MongoId, (int any, int fir)> map, MongoId tpl, int count, bool fir)
    {
        var current = map.GetValueOrDefault(tpl);
        map[tpl] = fir ? (current.any, current.fir + count) : (current.any + count, current.fir);
    }
}
