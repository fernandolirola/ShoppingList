using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using UnityEngine;

namespace ShoppingList.Client;

public partial class Plugin
{
    private const float W = 760, H = 820, Pad = 20, RowH = 40;
    private static readonly HashSet<string> MoneyTpls = new() { "5449016a4bdc2d6f028b456f", "5696686a4bdc2da3298b456a", "569668774bdc2da2298b4568", "5d235b4d86f7742e017bc88a" };

    private readonly IconCache _icons = new();
    private bool _confirming;
    private string _tooltip = "";

    private void DrawWindow(int id)
    {
        _tooltip = "";
        var ui = _ui;
        GUI.DrawTexture(new Rect(0, 0, W, H), ui.Bg);
        ui.Box(new Rect(0, 0, W, H), ui.Clear, ui.Line);
        GUI.DrawTexture(new Rect(0, 0, W, 3), ui.OrangeTex);

        DrawHeader();
        float top = 78;
        if (!_confirming)
        {
            DrawChips(new Rect(Pad, top, W - Pad * 2, 58));
            top += 70;
            DrawOptions(top);
            top += 32;
        }

        float footer = _confirming ? 104 : _status.Length > 0 ? 100 : 80;
        var body = new Rect(Pad, top, W - Pad * 2, H - top - footer);
        GUILayout.BeginArea(body);
        _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
        if (_plan is null)
        {
            GUILayout.Label(_status.Length > 0 ? _status : T("Loading...", "Carregando..."), ui.Center, GUILayout.Height(200));
        }
        else if (_confirming)
        {
            DrawConfirm(_plan, body.width - 20);
        }
        else
        {
            DrawPlan(_plan, body.width - 20);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        var footerRect = new Rect(Pad, H - footer, W - Pad * 2, footer - 16);
        GUI.DrawTexture(new Rect(0, H - footer - 1, W, 1), ui.Line);
        if (_confirming) DrawConfirmFooter(footerRect); else DrawFooter(footerRect);

        if (_tooltip.Length > 0)
        {
            var size = ui.Tooltip.CalcSize(new GUIContent(_tooltip));
            float width = Mathf.Min(size.x, 320);
            float height = ui.Tooltip.CalcHeight(new GUIContent(_tooltip), width);
            var mouse = Event.current.mousePosition;
            GUI.Label(new Rect(Mathf.Min(mouse.x + 14, W - width - 4), mouse.y + 18, width, height), _tooltip, ui.Tooltip);
        }

        GUI.DragWindow(new Rect(0, 0, W - 270, 70));
    }

    private void DrawHeader()
    {
        var ui = _ui;
        GUI.Label(new Rect(Pad, 16, 300, 14), "Z E Z I N H O ' S", ui.Brand);
        GUI.Label(new Rect(Pad, 32, 420, 30), T("SHOPPING LIST", "LISTA DE COMPRAS"), ui.Title);
        if (_focusArea is { } area)
        {
            var titleWidth = ui.Title.CalcSize(new GUIContent(T("SHOPPING LIST", "LISTA DE COMPRAS"))).x;
            GUI.Label(new Rect(Pad + titleWidth + 12, 36, 300, 26), "/  " + ((EAreaType)area).LocalizeAreaName(), ui.Subtitle);
        }
        else if (InRaid)
        {
            var titleWidth = ui.Title.CalcSize(new GUIContent(T("SHOPPING LIST", "LISTA DE COMPRAS"))).x;
            GUI.Label(new Rect(Pad + titleWidth + 12, 36, 200, 26), "/  " + T("In raid", "Na raid"), ui.Subtitle);
        }

        // All available, reset, refresh, language switch and close
        var all = new Rect(W - 474, 24, 100, 24);
        bool canAll = !_confirming && _focusArea is null && _selected is not null && _allAreas is not null && !InRaid;
        if (canAll && all.Contains(Event.current.mousePosition))
        {
            _tooltip = T("Plan the next level of every area that is not locked. Planned future levels stay.",
                "Planeja o próximo nível de todas as áreas não bloqueadas. Níveis futuros planejados continuam.");
        }

        if (canAll && ui.FlatButton(all, T("All available", "Tudo"), false, !_busy))
        {
            _selected!.UnionWith(_allAreas!.Where(a => !NextLocked(a)).Select(a => a.Type));
            Refresh();
        }

        var reset = new Rect(W - 368, 24, 100, 24);
        if (!_confirming && reset.Contains(Event.current.mousePosition))
        {
            _tooltip = T("Back to the start: next levels of the areas you can upgrade, every item ticked, nothing folded.",
                "Volta ao início: próximos níveis das áreas que você pode melhorar, todos os itens marcados, nada recolhido.");
        }

        if (!_confirming && ui.FlatButton(reset, T("Reset", "Redefinir"), false, !_busy))
        {
            _targets.Clear();
            _skipped.Clear();
            _collapsed.Clear();
            _targetState.Value = "";
            _collapsedState.Value = "";
            // Null lets the server pick the areas that are ready, like the first time the list opens
            _selected = null;
            _order = null;
            _scroll = Vector2.zero;
            Refresh();
        }

        if (!_confirming && ui.FlatButton(new Rect(W - 262, 24, 100, 24), _busy ? "..." : T("Refresh", "Atualizar"), false, !_busy))
        {
            Refresh();
        }

        var en = new Rect(W - 150, 24, 38, 24);
        var pt = new Rect(W - 112, 24, 38, 24);
        if (ui.FlatButton(en, "EN", !PT)) _portuguese.Value = false;
        if (ui.FlatButton(pt, "PT", PT)) _portuguese.Value = true;
        if (ui.FlatButton(new Rect(W - 56, 24, 34, 24), "✕", false))
        {
            _open = false;
        }
    }

    private void DrawChips(Rect r)
    {
        var ui = _ui;
        var plan = _plan;
        float gap = 10, w = (r.width - gap * 2) / 3;
        int items = plan is null ? 0 : ItemCount(plan.Purchases);
        bool enough = plan is null || !Shortfall(plan).Any();
        string rub = MoneyTpls.First();
        // Roubles stay big; dollars and euros go small on the label line so the card never overflows
        var others = plan is null ? new List<string>() : plan.Cost.Keys.Where(c => c != rub).ToList();
        long Have(string c) => plan!.Money.TryGetValue(c, out var have) ? have : 0;
        string Others(Func<string, long> amount) => string.Join("  ", others.Select(c => $"+ {Currency(c)}{Short(amount(c))}").ToArray());

        Chip(new Rect(r.x, r.y, w, r.height), T("ITEMS TO BUY", "ITENS A COMPRAR"), items.ToString(), "", Ui.Cream);
        Chip(new Rect(r.x + w + gap, r.y, w, r.height), T("TOTAL COST", "CUSTO TOTAL"),
            plan is null ? "-" : $"{Short(plan.Cost.TryGetValue(rub, out var cost) ? cost : 0)} ₽", plan is null ? "" : Others(c => plan.Cost[c]), Ui.Orange);
        Chip(new Rect(r.x + (w + gap) * 2, r.y, w, r.height), T("YOUR MONEY", "SEU DINHEIRO"),
            plan is null ? "-" : $"{Short(Have(rub))} ₽", plan is null ? "" : Others(Have), enough ? Ui.Green : Ui.Red);

        void Chip(Rect c, string label, string value, string extra, Color color)
        {
            ui.Box(c, ui.Card, ui.Line);
            GUI.DrawTexture(new Rect(c.x, c.y, 2, c.height), ui.Tint(color, 0.8f));
            GUI.Label(new Rect(c.x + 14, c.y + 9, c.width - 20, 14), label, ui.ChipLabel);
            var style = ui.Styled(ui.ChipValue, color);
            GUI.Label(new Rect(c.x + 14, c.y + 25, c.width - 20, 24), value, style);
            // Other currencies follow the roubles on the same line, smaller and dimmer
            float valueWidth = style.CalcSize(new GUIContent(value)).x;
            var small = ui.Styled(ui.Text, new Color(color.r, color.g, color.b, 0.7f), 13, clip: true);
            GUI.Label(new Rect(c.x + 14 + valueWidth + 8, c.y + 28, c.width - valueWidth - 30, 20), extra, small);
        }
    }

    // Dollars and euros are bought by the thousand, so a currency counts as one item, not one per unit
    private static int Units(string tpl, int count) => MoneyTpls.Contains(tpl) ? (count > 0 ? 1 : 0) : count;

    private static int ItemCount(IEnumerable<Purchase> purchases) => purchases.GroupBy(p => p.Tpl).Sum(g => Units(g.Key, g.Sum(p => p.Count)));

    // Roubles first, then the other currencies, the same way the cost card shows it
    private static string CostText(PlanResponse plan) =>
        string.Join(" + ", plan.Cost.OrderBy(c => c.Key == MoneyTpls.First() ? 0 : 1).Select(c => c.Key == MoneyTpls.First() ? $"{Short(c.Value)} ₽" : $"{Currency(c.Key)}{Short(c.Value)}").ToArray());

    private static IEnumerable<(string currency, long missing)> Shortfall(PlanResponse plan) =>
        // Dollars and euros bought in the same run count as money on hand
        plan.Cost.Select(c => (c.Key, c.Value - (plan.Money.TryGetValue(c.Key, out var have) ? have : 0) - plan.Purchases.Where(p => p.Tpl == c.Key).Sum(p => (long)p.Count)))
            .Where(s => s.Item2 > 0);

    // Built once per plan (and language): rows ask for these on every OnGUI call
    private Dictionary<string, (long unit, string source)> _offers = new();
    private Dictionary<int, List<AreaPlan>> _futureByArea = new();
    private PlanResponse? _offersFrom, _futureFrom;
    private bool _offersPt;

    // Cheapest unit price and where it comes from, for the list rows
    private (long unit, string source)? Offer(string tpl)
    {
        if (_raw != _offersFrom || PT != _offersPt)
        {
            _offersFrom = _raw;
            _offersPt = PT;
            _offers = (_raw?.Purchases ?? new List<Purchase>()).GroupBy(p => p.Tpl).ToDictionary(g => g.Key, g => (g.Min(UnitRub),
                string.Join(" + ", g.Select(p => p.Source == "trader" && p.Trader.Length > 0 ? TraderName(p.Trader) : T("flea market", "mercado")).Distinct().ToArray())));
        }

        return _offers.TryGetValue(tpl, out var offer) ? offer : null;
    }

    // Planned levels after the next one, nearest first
    private List<AreaPlan> FutureOf(int type)
    {
        if (_plan != _futureFrom)
        {
            _futureFrom = _plan;
            _futureByArea = (_plan?.Future ?? new List<AreaPlan>()).GroupBy(f => f.Type).ToDictionary(g => g.Key, g => g.OrderBy(f => f.Level).ToList());
        }

        return _futureByArea.TryGetValue(type, out var levels) ? levels : new List<AreaPlan>();
    }

    private static bool UpgradeReady(AreaPlan area) => area.Ready && area.Items.All(i => i.Missing == 0);

    // Planned levels of the area, the next one first
    private List<Need> PlannedNeeds(AreaPlan area) => area.Items.Concat(FutureOf(area.Type).SelectMany(f => f.Items)).ToList();

    private bool Buys(AreaPlan area) => PlannedNeeds(area).Any(n => n.Missing > 0 && n.Plan is "buy" or "partial" && !_skipped.Contains(n.Tpl));

    private string PlanHint(string plan) => plan switch
    {
        "buy" => T("Bought when you confirm the purchase.", "Comprado quando você confirmar a compra."),
        "partial" => T("Not enough offers for all of it right now. The rest has to wait.", "Não há ofertas para tudo agora. O resto vai ter que esperar."),
        "craft" => T("Bought as craft ingredients, listed under crafts.", "Comprado como ingredientes de craft, listados nos crafts."),
        "money" => T("Paid from your money when you start the upgrade.", "Pago com seu dinheiro quando você iniciar a melhoria."),
        "unavailable" => T("No trader or flea offer you can use right now (loyalty level, stock or flea level).", "Nenhuma oferta disponível agora (nível de lealdade, estoque ou nível do mercado)."),
        "later" => T("Bought when this level is the next upgrade.", "Comprado quando este nível for a próxima melhoria."),
        "ok" => T("You already have it.", "Você já tem."),
        // With crafts on, only later levels stay "craftable": their crafts wait until the level before is built
        "craftable" => _crafts.Value
            ? T("Must be found in raid, but you can craft it. Craft ingredients are bought for the next level only.", "Precisa ser encontrado em raid, mas você pode craftar. Ingredientes de craft são comprados só para o próximo nível.")
            : T("Must be found in raid, but you can craft it. Turn on Craft ingredients to buy what the craft needs.", "Precisa ser encontrado em raid, mas você pode craftar. Ligue Ingredientes de craft para comprar o que o craft precisa."),
        _ => T("Must be found in raid, and you have no unlocked craft for it.", "Precisa ser encontrado em raid, e você não tem craft desbloqueado para ele."),
    };

    // Runs the stash already covers: tools are needed once, items per run
    private static int CraftableRuns(CraftPlan craft)
    {
        int runs = craft.Runs;
        foreach (var ing in craft.Ingredients)
        {
            if (ing.Tool)
            {
                if (ing.Have < ing.Required) return 0;
                continue;
            }

            int perRun = Math.Max(1, ing.Required / Math.Max(1, craft.Runs));
            runs = Math.Min(runs, ing.Have / perRun);
        }

        return runs;
    }

    private readonly HashSet<string> _collapsed = new();
    // Craft icon x (12 + tick box 26 + arrow 18) minus the row's own 8 px padding
    private const float CraftIndent = 48;
    // Item icons line up under the area icon (header x 12 + arrow 18) minus the row's own 8 px padding
    private const float AreaIndent = 22;

    // Draws the open/closed arrow at x and makes the rest of the header a toggle; returns whether it is closed
    private bool Collapsible(Rect header, ref float x, string key)
    {
        // The hover covers the whole header, left padding included
        bool collapsed = Collapsible(header, x, key);
        x += 18;
        return collapsed;
    }

    // `closed` keeps it folded without a toggle, for skipped areas
    private bool Collapsible(Rect hit, float arrowX, string key, bool closed = false)
    {
        bool collapsed = closed || _collapsed.Contains(key);
        if (!closed && hit.Contains(Event.current.mousePosition)) GUI.DrawTexture(hit, _ui.Hover);
        GUI.Label(new Rect(arrowX, hit.y, 14, hit.height), collapsed ? "►" : "▼", _ui.Chevron);
        if (!closed && GUI.Button(hit, GUIContent.none, GUIStyle.none))
        {
            if (!_collapsed.Remove(key)) _collapsed.Add(key);
            _collapsedState.Value = string.Join(",", _collapsed.OrderBy(k => k).ToArray());
        }

        return collapsed;
    }

    private bool _sortDesc = true;
    private List<int>? _order;

    private void DrawOptions(float top)
    {
        var ui = _ui;
        GUI.Label(new Rect(Pad, top, 60, 24), T("Sort by", "Ordenar"), ui.Muted);
        float x = Pad + 60;
        foreach (var (mode, label) in new[] { (SortMode.Default, T("Default", "Padrão")), (SortMode.Progress, T("Progress %", "Progresso %")), (SortMode.Rarity, T("Rarity", "Raridade")) })
        {
            bool active = _sort.Value == mode;
            string text = active && mode != SortMode.Default ? label + (_sortDesc ? "  ▼" : "  ▲") : label;
            if (ui.FlatButton(new Rect(x, top, 90, 24), text, active))
            {
                if (active) _sortDesc = !_sortDesc; else { _sort.Value = mode; _sortDesc = true; }
                _order = null;
            }

            x += 96;
        }

        float hx = x + 18;
        bool hide = ui.Checkbox(new Rect(hx, top + 1, 24, 22), _hideReady.Value);
        GUI.Label(new Rect(hx + 24, top + 1, 140, 22), T("Hide ready items", "Esconder prontos"), ui.Text);
        if (hide != _hideReady.Value)
        {
            _hideReady.Value = hide;
        }

        // Right-aligned by its real width, so the two checkboxes keep a clear gap in both languages
        string craftText = T("Craft ingredients", "Ingredientes de craft");
        float fx = W - Pad - 24 - ui.Text.CalcSize(new GUIContent(craftText)).x;
        bool crafts = ui.Checkbox(new Rect(fx, top + 1, 24, 22), _crafts.Value);
        var craftLabel = new Rect(fx + 24, top + 1, W - Pad - fx - 24, 22);
        GUI.Label(craftLabel, T("Craft ingredients", "Ingredientes de craft"), ui.Text);
        if (craftLabel.Contains(Event.current.mousePosition))
        {
            _tooltip = T("For found-in-raid items you can craft, buy the craft's ingredients.", "Para itens FIR que você pode craftar, compra os ingredientes do craft.");
        }

        if (crafts != _crafts.Value)
        {
            _crafts.Value = crafts;
            Refresh();
        }
    }

    // Share of each requirement already covered, averaged so a big rouble count does not outweigh the items
    private static float Progress(AreaPlan area) =>
        area.Items.Count == 0 ? 1 : area.Items.Average(i => i.Required == 0 ? 1f : 1f - (float)i.Missing / i.Required);

    private static int RarityKey(AreaPlan area)
    {
        var missing = area.Items.Where(i => i.Missing > 0).ToList();
        if (missing.Count == 0) return -10000;
        int top = missing.Max(i => i.Rarity);
        return top * 1000 + missing.Count(i => i.Rarity == top);
    }

    private List<AreaPlan> Ordered(IEnumerable<AreaPlan> areas, Func<AreaPlan, bool> on)
    {
        var list = areas.ToList();
        // Areas you can upgrade right now come first, whatever the sort, and left-out locked ones last
        // then, by default, the areas the money goes to
        var ordered = list.OrderByDescending(on).ThenBy(a => !on(a) && NextLocked(a)).ThenByDescending(UpgradeReady);
        if (_sort.Value == SortMode.Default) return ordered.ThenByDescending(Buys).ToList();
        Func<AreaPlan, float> key = _sort.Value == SortMode.Progress ? Progress : a => RarityKey(a);
        return (_sortDesc ? ordered.ThenByDescending(key) : ordered.ThenBy(key)).ToList();
    }

    private bool NextLocked(AreaPlan area) => Open(area, area.Locks).Count > 0;

    private List<Need> Rows(AreaPlan area)
    {
        var rows = _hideReady.Value ? area.Items.Where(i => i.Missing > 0).ToList() : area.Items;
        return _sort.Value != SortMode.Rarity
            ? rows
            : rows.OrderByDescending(i => i.Missing > 0).ThenBy(i => _sortDesc ? -i.Rarity : i.Rarity).ToList();
    }

    private void DrawPlan(PlanResponse plan, float width)
    {
        var ui = _ui;
        if (plan.Areas.Count == 0 && _allAreas is not { Count: > 0 })
        {
            GUILayout.Label(T("Nothing to upgrade right now.", "Nada para melhorar agora."), ui.Center, GUILayout.Height(160));
        }

        // Unticked areas are left out of the plan, so the overview keeps them from the first full plan
        bool overview = _focusArea is null && _selected is not null && _allAreas is not null;
        var areas = overview
            ? _allAreas!.Select(a => plan.Areas.FirstOrDefault(p => p.Type == a.Type) ?? a).ToList()
            : plan.Areas;
        // Sorted when the list opens or the sort changes, not on every click, so an area never jumps away from the cursor
        if (_order is null || areas.Any(a => !_order.Contains(a.Type)))
        {
            _order = Ordered(areas, a => !overview || _selected!.Contains(a.Type)).Select(a => a.Type).ToList();
        }

        foreach (var area in areas.OrderBy(a => _order.IndexOf(a.Type)))
        {
            DrawArea(area, FutureOf(area.Type), width, overview, "a" + area.Type);
        }

        if (plan.Crafts.Count > 0)
        {
            var header = GUILayoutUtility.GetRect(width, 38);
            ui.Box(header, ui.Card, ui.Line);
            GUI.DrawTexture(new Rect(header.x, header.y, 2, header.height), ui.OrangeTex);
            float hx = header.x + 12;
            bool craftsCollapsed = Collapsible(header, ref hx, "crafts");
            GUI.Label(new Rect(hx, header.y, width, header.height), T("CRAFTS FOR FOUND-IN-RAID ITEMS", "CRAFTS PARA ITENS FIR"), ui.Header);
            foreach (var craft in craftsCollapsed ? [] : plan.Crafts)
            {
                GUILayout.Space(6);
                var sub = GUILayoutUtility.GetRect(width, RowH);
                ui.Box(sub, ui.Tint(Ui.Cream, 0.05f), ui.Line);
                // Same order as area headers: tick box, arrow, then the icon its ingredients line up under
                float cx = sub.x + 12;
                var buyable = craft.Ingredients.Where(i => i.Plan is "buy" or "partial").Select(i => i.Tpl).ToList();
                if (buyable.Count > 0)
                {
                    bool on = buyable.Any(t => !_skipped.Contains(t));
                    if (ui.Checkbox(new Rect(cx, sub.y, 24, sub.height), on) != on)
                    {
                        foreach (var t in buyable)
                        {
                            if (on) _skipped.Add(t); else _skipped.Remove(t);
                        }

                        ApplySkips();
                    }
                }

                cx += 26;
                int canRun = CraftableRuns(craft);
                // Drawn before the fold toggle so its click wins; it stands out once the stash covers a run
                var go = new Rect(sub.xMax - 144 - 122, sub.y + 10, 114, 20);
                if (InHideout && ui.FlatButton(go, T("GO TO STATION", "IR PARA ESTAÇÃO"), canRun > 0))
                {
                    GoToArea(craft.Area);
                }

                if (InHideout && go.Contains(Event.current.mousePosition))
                {
                    _tooltip = T($"Open {((EAreaType)craft.Area).LocalizeAreaName()} to start the craft.", $"Abre {((EAreaType)craft.Area).LocalizeAreaName()} para iniciar o craft.");
                }

                bool craftCollapsed = Collapsible(sub, ref cx, "c" + craft.Tpl);
                DrawIcon(new Rect(cx, sub.y + 3, RowH - 6, RowH - 6), craft.Tpl);
                GUI.Label(new Rect(cx + RowH + 6, sub.y, go.x - cx - RowH - 12, sub.height),
                    $"<b>{Name(craft.Tpl)} x{craft.Missing}</b>   <color=#9a9a94>{((EAreaType)craft.Area).LocalizeAreaName()}  ·  {craft.Runs} {(craft.Runs == 1 ? T("run", "vez") : T("runs", "vezes"))}</color>", ui.Text);
                var (craftLabel, craftColor) = canRun >= craft.Runs
                    ? (T($"CAN CRAFT x{craft.Runs}", $"PODE CRAFTAR x{craft.Runs}"), Ui.Green)
                    : canRun > 0
                        ? (T($"CAN CRAFT {canRun} OF {craft.Runs}", $"PODE CRAFTAR {canRun} DE {craft.Runs}"), Ui.Orange)
                        : (T("BUY INGREDIENTS", "COMPRAR INGREDIENTES"), Ui.Grey);
                ui.PillLabel(new Rect(sub.xMax - 144, sub.y + 10, 136, 20), craftLabel, craftColor);
                var ingredients = _hideReady.Value ? craft.Ingredients.Where(n => n.Missing > 0).ToList() : craft.Ingredients;
                for (int i = 0; !craftCollapsed && i < ingredients.Count; i++)
                {
                    DrawNeedRow(ingredients[i], width, CraftIndent, i % 2 == 1);
                }
            }

            GUILayout.Space(12);
        }

        if (!plan.FleaUnlocked)
        {
            GUILayout.Label(T("Flea market is locked for your level, only traders are used.", "Mercado bloqueado no seu nível, só comerciantes."), ui.Muted);
        }
    }

    private void DrawArea(AreaPlan area, List<AreaPlan> future, float width, bool overview, string key)
    {
        var ui = _ui;
        bool on = !overview || _selected!.Contains(area.Type);
        var header = GUILayoutUtility.GetRect(width, 42);
        ui.Box(header, on ? ui.Card : ui.Clear, ui.Line);

        float arrowX = header.x + 12;
        // Centered on the item icon boxes below (34 px wide at the same x), not just left-aligned with them
        var icon = new Rect(arrowX + 19, header.y + 5, 32, 32);
        float x = icon.xMax + 8;
        string name = ((EAreaType)area.Type).LocalizeAreaName();
        var nameStyle = on ? ui.Header : ui.Styled(ui.Header, Ui.Grey);
        float nameWidth = nameStyle.CalcSize(new GUIContent(name)).x;

        // The track's buttons come before the fold toggle so their clicks win
        float trackX = Mathf.Round(Mathf.Max(x + nameWidth + 16, header.x + 270));
        DrawTrack(new Rect(trackX, header.y, 0, header.height), area, future, overview, on);
        bool collapsed = Collapsible(header, arrowX, key, closed: !on);

        var color = GUI.color;
        if (!on) GUI.color = new Color(1, 1, 1, 0.5f);
        IconCache.DrawSprite(icon, AreaIcons.Get(area.Type));
        GUI.color = color;

        // The nearest level still blocked, shown under the name so nobody has to hover the track
        var blocked = new[] { area }.Concat(future).Select(l => (level: l, locks: Open(area, l.Locks))).FirstOrDefault(l => l.locks.Count > 0);
        if (blocked.level is null)
        {
            GUI.Label(new Rect(x, header.y, nameWidth, header.height), name, nameStyle);
        }
        else
        {
            GUI.Label(new Rect(x, header.y + 3, nameWidth, 20), name, nameStyle);
            var why = ui.Styled(ui.Muted, on ? Ui.Red : Ui.Grey, clip: true);
            string prefix = blocked.level == area ? "" : $"{T("Level", "Nível")} {blocked.level.Level} ";
            string text = prefix + T("needs", "precisa de") + " " + LockText(blocked.locks);
            // Stops before the track; the whole text is in the tooltip when it does not fit
            var whyRect = new Rect(x, header.y + 22, trackX - x - 10, 16);
            GUI.Label(whyRect, text, why);
            if (why.CalcSize(new GUIContent(text)).x > whyRect.width && whyRect.Contains(Event.current.mousePosition)) _tooltip = text;
        }

        var planned = PlannedNeeds(area);
        int ready = planned.Count(i => i.Missing == 0);
        float progress = planned.Count == 0 ? 1 : planned.Average(i => i.Required == 0 ? 1f : 1f - (float)i.Missing / i.Required);
        // Ends where the rows' status pills end, so the right edge lines up
        var bar = new Rect(header.xMax - 104, header.y + 19, 96, 4);
        GUI.DrawTexture(bar, ui.Line);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), !on ? ui.Tint(Ui.Grey, 0.6f) : ready == planned.Count ? ui.Tint(Ui.Green, 1) : ui.OrangeTex);
        var countColor = Ui.Grey;
        // What the planned levels still cost, from the cheapest offers; ticked-off items are left out
        long cost = planned.Where(n => n.Plan is "buy" or "partial" && !_skipped.Contains(n.Tpl)).Sum(n => (Offer(n.Tpl)?.unit ?? 0) * n.Missing);
        string summary;
        if (!on)
        {
            summary = NextLocked(area) ? T("locked", "bloqueada") : T("skipped", "ignorada");
        }
        else if (UpgradeReady(area) && future.Count == 0)
        {
            countColor = Ui.Green;
            summary = T("READY TO UPGRADE", "PRONTO PARA MELHORAR");
        }
        else if (cost > 0)
        {
            countColor = Ui.Cream;
            summary = $"~{Short(cost)} ₽ {T("to buy", "a comprar")}  ·  {Mathf.FloorToInt(progress * 100)}%";
        }
        else
        {
            // Say what is left to do instead of just that nothing is bought
            int Left(params string[] plans) => planned.Where(n => n.Missing > 0 && plans.Contains(n.Plan)).Sum(n => n.Missing);
            int fir = Left("none"), craft = Left("craft", "craftable"), noOffer = Left("unavailable", "partial");
            var money = planned.Where(n => n.Missing > 0 && n.Plan == "money").GroupBy(n => n.Tpl).Select(g => $"{Currency(g.Key)}{Short(g.Sum(n => n.Missing))}").ToArray();
            string left = money.Length > 0 ? T($"{string.Join(" + ", money)} short", $"faltam {string.Join(" + ", money)}")
                : fir > 0 ? T($"{fir} to find in raid", $"{fir} para achar em raid")
                : craft > 0 ? T($"{craft} to craft", $"{craft} para craftar")
                : noOffer > 0 ? T($"{noOffer} not for sale", $"{noOffer} sem oferta")
                : T("nothing to buy", "nada a comprar");
            summary = $"{left}  ·  {Mathf.FloorToInt(progress * 100)}%";
        }

        GUI.Label(new Rect(bar.x - 194, header.y, 180, header.height), summary, ui.Styled(ui.Muted, countColor, align: TextAnchor.MiddleRight));

        if (collapsed)
        {
            GUILayout.Space(6);
            return;
        }

        // Level dividers only help once more than one level is planned
        bool levels = future.Count > 0;
        foreach (var level in new[] { area }.Concat(future))
        {
            if (levels) LevelDivider(level, Open(area, level.Locks), width);
            var rows = Rows(level);
            for (int i = 0; i < rows.Count; i++)
            {
                DrawNeedRow(rows[i], width, AreaIndent, i % 2 == 1, on);
            }
        }

        GUILayout.Space(12);
    }

    // Only the area's own earlier levels are left out; other areas stay listed even when they are planned
    private static List<Lock> Open(AreaPlan owner, List<Lock> locks) =>
        locks.Where(l => l.Kind != "area" || l.Area != owner.Type).ToList();

    private int Target(AreaPlan area) =>
        Mathf.Clamp(_targets.TryGetValue(area.Type, out var level) ? level : area.Level, area.Level, Math.Max(area.Level, area.MaxLevel));

    private string LockText(List<Lock> locks) => string.Join(", ", locks.Select(l => l.Kind == "area"
        ? $"{((EAreaType)l.Area).LocalizeAreaName()} {l.Level}"
        : $"{TraderName(l.Trader)} LL{l.Level}").ToArray());

    private void LevelDivider(AreaPlan level, List<Lock> locks, float width)
    {
        var r = GUILayoutUtility.GetRect(width, 24);
        GUI.DrawTexture(r, _ui.RowAlt);
        string text = $"{T("LEVEL", "NÍVEL")} {level.Level}";
        if (locks.Count > 0) text += $"   <color=#{ColorUtility.ToHtmlStringRGB(Ui.Red)}>{T("needs", "precisa de")} {LockText(locks)}</color>";
        GUI.Label(new Rect(r.x + 8 + AreaIndent, r.y, r.width - 16 - AreaIndent, r.height), text, _ui.Muted);
    }

    // Built levels, then the planned ones up to the target; clicking a level plans up to it
    private void DrawTrack(Rect r, AreaPlan area, List<AreaPlan> future, bool overview, bool on)
    {
        var ui = _ui;
        const float dot = 20, step = 30;
        int built = area.Level - 1, max = Math.Max(area.Level, area.MaxLevel);
        int target = on ? Target(area) : built;
        // Whole pixels, so the shapes are drawn 1:1 and stay sharp
        float y = Mathf.Round(r.y + (r.height - dot) / 2);
        for (int level = 1; level <= max; level++)
        {
            var d = new Rect(r.x + (level - 1) * step, y, dot, dot);
            bool done = level <= built, planned = level > built && level <= target;
            if (level > 1)
            {
                var link = done ? ui.Tint(Ui.Green, 0.8f) : planned ? ui.OrangeTex : ui.Line;
                GUI.DrawTexture(new Rect(d.x - (step - dot), d.y + dot / 2 - 1, step - dot, 2), link);
            }

            var locks = Open(area, level == area.Level ? area.Locks : future.FirstOrDefault(f => f.Level == level)?.Locks ?? new List<Lock>());
            bool locked = locks.Count > 0 && !done;
            var dark = new Color(0.07f, 0.07f, 0.07f);
            if (done || planned)
            {
                Ui.Tinted(d, ui.Circle, done ? Color.Lerp(Ui.Green, Color.black, 0.15f) : Ui.Orange);
            }
            else
            {
                Ui.Tinted(d, ui.Circle, new Color(0.095f, 0.095f, 0.095f));
                Ui.Tinted(d, ui.Ring, locked ? Ui.Red : new Color(Ui.Cream.r, Ui.Cream.g, Ui.Cream.b, 0.35f));
            }

            if (done)
            {
                Ui.Tinted(new Rect(d.x + 4, d.y + 4, dot - 8, dot - 8), ui.Check, dark);
            }
            else if (locked)
            {
                Ui.Tinted(new Rect(d.x + 5, d.y + 5, dot - 10, dot - 10), ui.Lock, planned ? dark : Ui.Red);
            }
            else
            {
                GUI.Label(d, level.ToString(), ui.Styled(ui.Badge, planned ? dark : Ui.Grey));
            }

            if (d.Contains(Event.current.mousePosition))
            {
                string state = done ? T("built", "construído") : planned ? T("planned", "planejado") : level == area.Level ? T("click to plan", "clique para planejar") : T("click to plan up to here", "clique para planejar até aqui");
                _tooltip = $"<b>{T("Level", "Nível")} {level}</b>  ·  {state}";
                if (locks.Count > 0 && !done) _tooltip += $"\n<color=#{ColorUtility.ToHtmlStringRGB(Ui.Red)}>{T("Needs", "Precisa de")} {LockText(locks)}</color>";
                if (planned && level == target)
                {
                    _tooltip += "\n" + (level > area.Level ? T("Click again to unplan this level.", "Clique de novo para desplanejar este nível.")
                        : overview ? T("Click again to skip this area.", "Clique de novo para ignorar esta área.") : "");
                }
            }

            if (done || !GUI.Button(d, GUIContent.none, GUIStyle.none))
            {
                continue;
            }

            // Clicking the last planned level steps back one; any other level plans up to it
            int wanted = on && level == target ? level - 1 : level;
            if (wanted < area.Level)
            {
                if (overview) _selected!.Remove(area.Type);
                _targets.Remove(area.Type);
            }
            else
            {
                if (overview) _selected!.Add(area.Type);
                if (wanted == area.Level) _targets.Remove(area.Type); else _targets[area.Type] = wanted;
            }

            _targetState.Value = string.Join(",", _targets.OrderBy(t => t.Key).Select(t => $"{t.Key}:{t.Value}").ToArray());
            Refresh();
        }
    }

    private void DrawNeedRow(Need need, float width, float indent, bool alt, bool purchasing = true)
    {
        var ui = _ui;
        var r = GUILayoutUtility.GetRect(width, RowH);
        if (alt) GUI.DrawTexture(r, ui.RowAlt);
        if (r.Contains(Event.current.mousePosition)) GUI.DrawTexture(r, ui.Hover);

        float x = r.x + 8 + indent;
        DrawIcon(new Rect(x, r.y + 3, RowH - 6, RowH - 6), need.Tpl);
        x += RowH + 6;

        // Name on top, details below: tags first, then the price for rows that are bought
        bool buyable = purchasing && need.Plan is ("buy" or "partial");
        var details = new List<string>();
        if (need.Fir) details.Add("<color=#F2AA3C><b>FIR</b></color>");
        if (need.Tool) details.Add(T("tool", "ferramenta"));
        if (need.Missing > 0 && !MoneyTpls.Contains(need.Tpl))
        {
            string rarity = need.Rarity switch
            {
                2 => $"<color=#C792EA>{T("very rare", "muito raro")}</color>",
                1 => $"<color=#8FB8E8>{T("rare", "raro")}</color>",
                0 => T("common", "comum"),
                _ => "",
            };
            if (rarity.Length > 0) details.Add(rarity);
        }

        if (buyable && Offer(need.Tpl) is { } offer) details.Add($"{Short(offer.unit)} ₽ {T("each", "cada")}  ·  {offer.source}");
        if (details.Count > 0)
        {
            GUI.Label(new Rect(x, r.y + 3, 300 - indent, 20), $"<b>{ListName(need.Tpl)}</b>", ui.Text);
            GUI.Label(new Rect(x, r.y + 21, 330 - indent, 16), string.Join("  ·  ", details.ToArray()), ui.Muted);
        }
        else
        {
            GUI.Label(new Rect(x, r.y, 300 - indent, r.height), $"<b>{ListName(need.Tpl)}</b>", ui.Text);
        }

        bool money = MoneyTpls.Contains(need.Tpl);
        int have = need.Have + need.Donated;
        string haveColor = need.Missing == 0 ? "#78CC6B" : "#EFEBE0";
        string amount = money ? $"{Short(need.Required)}" : $"<color={haveColor}><b>{have}</b></color><color=#9a9a94> / {need.Required}</color>";
        GUI.Label(new Rect(r.x + 380, r.y, 110, r.height), amount, ui.Text);
        if (need.Carried > 0)
        {
            GUI.Label(new Rect(r.x + 440, r.y, 86, r.height), $"{need.Carried} {T("on you", "com você")}", ui.Styled(ui.Muted, Ui.Green));
        }
        else if (need.Donated > 0)
        {
            GUI.Label(new Rect(r.x + 440, r.y, 80, r.height), $"{need.Donated} {T("donated", "doado")}", ui.Muted);
        }

        bool skipped = buyable && _skipped.Contains(need.Tpl);
        if (buyable && ui.Checkbox(new Rect(r.xMax - 172, r.y, 22, r.height), !skipped) == skipped)
        {
            if (!_skipped.Remove(need.Tpl)) _skipped.Add(need.Tpl);
            ApplySkips();
        }

        var (label, color) = !purchasing && need.Missing > 0 ? (T("NOT SELECTED", "NÃO SELECIONADO"), Ui.Grey) : skipped ? (T("SKIPPED", "IGNORADO"), Ui.Grey) : need.Plan switch
        {
            "ok" => (T("READY", "PRONTO"), Ui.Green),
            "buy" => (T("BUY", "COMPRAR"), Ui.Orange),
            "partial" => (T("PARTLY FOR SALE", "OFERTA PARCIAL"), Ui.Orange),
            "craft" => (T("CRAFT", "CRAFT"), Ui.Cream),
            "craftable" => (T("CRAFTABLE", "CRAFTÁVEL"), Ui.Cream),
            "money" => need.Missing > 0 ? (T("NEED MONEY", "FALTA DINHEIRO"), Ui.Red) : (T("READY", "PRONTO"), Ui.Green),
            "unavailable" => (T("NOT FOR SALE", "SEM OFERTA"), Ui.Grey),
            "later" => (T("BUY LATER", "COMPRAR DEPOIS"), Ui.Grey),
            _ => (T("FIND IN RAID", "ENCONTRADO EM RAID"), Ui.Amber),
        };
        var pill = new Rect(r.xMax - 144, r.y + 10, 136, 20);
        // Only what you act on stands out: buying, and money that is short
        bool loud = !skipped && purchasing && (buyable || need.Plan == "money" && need.Missing > 0);
        if (loud) ui.PillLabel(pill, label, color); else ui.QuietPill(pill, label, color);
        if (pill.Contains(Event.current.mousePosition))
        {
            _tooltip = !purchasing && need.Missing > 0
                ? T("This area is unticked, so nothing is bought for it.", "Esta área está desmarcada, nada é comprado para ela.")
                : skipped ? T("Unticked, so it is left out of the purchase.", "Desmarcado, fica fora da compra.") : PlanHint(need.Plan);
        }
    }

    private void DrawIcon(Rect r, string tpl)
    {
        var ui = _ui;
        ui.Box(r, ui.Cell, ui.CellBorder);
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (MoneyTpls.Contains(tpl))
        {
            GUI.Label(r, Currency(tpl), ui.Styled(ui.Header, Ui.Orange, 18, TextAnchor.MiddleCenter));
            return;
        }

        _icons.Draw(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), tpl);
    }

    private void DrawFooter(Rect r)
    {
        var ui = _ui;
        var plan = _plan;
        int items = plan is null ? 0 : ItemCount(plan.Purchases);
        bool enough = plan is null || !Shortfall(plan).Any();
        string shortBy = plan is null ? "" : string.Join(" + ", Shortfall(plan).Select(s => $"{Short(s.missing)} {Currency(s.currency)}").ToArray());
        bool can =!_busy && plan is not null && enough && plan.Operations.Count > 0 && !InRaid;

        string text = InRaid
            ? T("BUYING WORKS IN THE HIDEOUT", "COMPRAS SÓ NO ESCONDERIJO")
            : items == 0
            ? T("NOTHING TO BUY", "NADA PARA COMPRAR")
            : !enough ? T($"NOT ENOUGH MONEY  ·  SHORT {shortBy}", $"DINHEIRO INSUFICIENTE  ·  FALTAM {shortBy}")
            : T($"BUY {items} ITEMS  ·  {CostText(plan!)}", $"COMPRAR {items} ITENS  ·  {CostText(plan!)}");
        // Same gap below as the window's side padding
        if (ui.FlatButton(new Rect(r.x, H - Pad - 46, r.width, 46), text, true, can))
        {
            _confirming = true;
            _run = null;
            _scroll = Vector2.zero;
            _status = "";
        }

        if (_status.Length > 0)
        {
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 18), _status, ui.Muted);
        }
    }

    private sealed class ConfirmLine
    {
        public string Tpl = "";
        public int Count;
        public long Rub;
        public string Detail = "";
        public string Source = "";
    }

    // One section of the review screen: an area's planned levels, or the craft ingredients
    private sealed class ConfirmGroup
    {
        public string Title = "";
        public int? Area;
        public string DetailPrefix = "";
        public Color Accent;
        public readonly List<ConfirmLine> Lines = new();
    }

    // Splits what will be bought by the area it is donated to, in list order, with craft ingredients last.
    // Units go to the nearest level first, the same order the server reserves them in
    private List<ConfirmGroup> BuildConfirm(PlanResponse plan)
    {
        var bought = plan.Purchases.GroupBy(p => p.Tpl).ToDictionary(g => g.Key, g => (
            count: g.Sum(p => p.Count),
            rub: g.Sum(p => p.Rub),
            source: string.Join(" + ", g.Select(p => p.Source == "trader" && p.Trader.Length > 0 ? TraderName(p.Trader) : T("flea market", "mercado")).Distinct().ToArray())));

        var levels = plan.Areas.Concat(plan.Future.OrderBy(a => a.Level)).ToList();
        var groups = new Dictionary<int, ConfirmGroup>();
        var craft = new ConfirmGroup { Title = T("CRAFT INGREDIENTS", "INGREDIENTES DE CRAFT"), DetailPrefix = T("To craft", "Para craftar"), Accent = Ui.Cream };
        var currency = new ConfirmGroup { Title = T("CURRENCY TO PAY WITH", "MOEDA PARA PAGAR"), DetailPrefix = T("Pays for", "Paga"), Accent = Ui.Green };
        foreach (var pair in bought)
        {
            string tpl = pair.Key;
            var (count, rub, source) = pair.Value;
            int left = count;
            var taken = new Dictionary<int, (int count, List<int> levels)>();
            foreach (var level in levels)
            {
                foreach (var need in level.Items.Where(i => i.Tpl == tpl && i.Missing > 0 && i.Plan is "buy" or "partial"))
                {
                    int take = Math.Min(left, need.Missing);
                    if (take <= 0) continue;
                    left -= take;
                    var t = taken.TryGetValue(level.Type, out var known) ? known : (0, new List<int>());
                    if (!t.Item2.Contains(level.Level)) t.Item2.Add(level.Level);
                    taken[level.Type] = (t.Item1 + take, t.Item2);
                }
            }

            foreach (var (type, (n, lv)) in taken)
            {
                if (!groups.TryGetValue(type, out var group))
                {
                    groups[type] = group = new ConfirmGroup { Area = type, DetailPrefix = T("Donate to", "Doar para"), Accent = Ui.Orange };
                }

                string name = ((EAreaType)type).LocalizeAreaName();
                group.Lines.Add(new ConfirmLine { Tpl = tpl, Count = n, Rub = rub * n / count, Source = source, Detail = $"{name} {string.Join(", ", lv.Select(l => l.ToString()).ToArray())}" });
            }

            if (left > 0 && MoneyTpls.Contains(tpl))
            {
                currency.Lines.Add(new ConfirmLine { Tpl = tpl, Count = left, Rub = rub * left / count, Source = source, Detail = T("items priced in this currency", "itens cobrados nesta moeda") });
            }
            else if (left > 0)
            {
                var forCrafts = plan.Crafts.Where(c => c.Ingredients.Any(i => i.Tpl == tpl && i.Missing > 0));
                craft.Lines.Add(new ConfirmLine
                {
                    Tpl = tpl, Count = left, Rub = rub * left / count, Source = source,
                    Detail = string.Join(", ", forCrafts.Select(c => $"{Name(c.Tpl)} ({((EAreaType)c.Area).LocalizeAreaName()})").Distinct().ToArray()),
                });
            }
        }

        foreach (var group in groups.Values)
        {
            var planned = levels.Where(l => l.Type == group.Area).Select(l => l.Level).ToList();
            string range = planned.Min() == planned.Max() ? $"{planned.Min()}" : T($"{planned.Min()} to {planned.Max()}", $"{planned.Min()} a {planned.Max()}");
            group.Title = $"{((EAreaType)group.Area!.Value).LocalizeAreaName().ToUpperInvariant()}  <color=#9a9a94>{T("level", "nível")} {range}</color>";
        }

        var order = _order ?? new List<int>();
        var result = groups.Values.OrderBy(g => order.Contains(g.Area!.Value) ? order.IndexOf(g.Area.Value) : int.MaxValue).ToList();
        if (craft.Lines.Count > 0) result.Add(craft);
        // Bought first, so it is listed first
        if (currency.Lines.Count > 0) result.Insert(0, currency);
        return result;
    }

    // The review sections only change with the plan, so they are not rebuilt on every OnGUI call
    private List<ConfirmGroup> _confirm = new();
    private PlanResponse? _confirmFrom;
    private bool _confirmPt;

    private void DrawConfirm(PlanResponse plan, float width)
    {
        var run = _run;
        if (run is null && (plan != _confirmFrom || PT != _confirmPt))
        {
            _confirm = BuildConfirm(plan);
            _confirmFrom = plan;
            _confirmPt = PT;
        }

        var groups = run is null ? _confirm : run.Groups;
        var titleRow = GUILayoutUtility.GetRect(width, 32);
        GUI.Label(titleRow, T("REVIEW PURCHASE", "REVISAR COMPRA"), _ui.Title);
        // Where it matters: right before buying, not only in F12
        string retryText = T("Retry sold-out offers", "Tentar de novo ofertas esgotadas");
        float retryWidth = _ui.Text.CalcSize(new GUIContent(retryText)).x;
        var retryBox = new Rect(titleRow.xMax - retryWidth - 24, titleRow.y + 5, 24, 22);
        bool auto = _ui.Checkbox(retryBox, _autoRetry.Value);
        var retryLabel = new Rect(retryBox.xMax, titleRow.y + 5, retryWidth, 22);
        GUI.Label(retryLabel, retryText, _ui.Text);
        if (auto != _autoRetry.Value && run is not { Running: true }) _autoRetry.Value = auto;
        if (retryBox.Contains(Event.current.mousePosition) || retryLabel.Contains(Event.current.mousePosition))
        {
            _tooltip = T($"When an offer sells out while buying, buy the next one if it costs at most {_priceLimit.Value}% more. Off: the item stops so you can Retry or Skip it.",
                $"Quando uma oferta esgota durante a compra, compra a próxima se custar no máximo {_priceLimit.Value}% a mais. Desligado: o item para para você Tentar ou Ignorar.");
        }

        GUILayout.Label(T("Hover an item to see what it is for and where it comes from.", "Passe o mouse num item para ver para que serve e de onde vem."), _ui.Muted, GUILayout.Height(18));
        GUILayout.Space(12);
        // Bought units fill the sections in order, so each section counts what the earlier ones took
        var before = new Dictionary<string, int>();
        foreach (var group in groups)
        {
            var offset = new Dictionary<string, int>(before);
            DrawTiles(group, width, run, tpl => offset.TryGetValue(tpl, out var n) ? n : 0);
            foreach (var line in group.Lines)
            {
                before[line.Tpl] = (before.TryGetValue(line.Tpl, out var n) ? n : 0) + line.Count;
            }
        }
    }

    private void DrawTiles(ConfirmGroup group, float width, BuyRun? run, Func<string, int> boughtBefore)
    {
        var lines = group.Lines;
        string title = group.Title, detailPrefix = group.DetailPrefix;
        var accent = group.Accent;
        if (lines.Count == 0)
        {
            return;
        }

        BuyState? State(ConfirmLine line) => run is not null && run.Items.TryGetValue(line.Tpl, out var s) ? s : null;
        int Got(ConfirmLine line) => State(line) is { } s ? Mathf.Clamp(s.Bought - boughtBefore(line.Tpl), 0, line.Count) : 0;
        long Paid(ConfirmLine line) => State(line) is { Bought: > 0 } s ? s.SpentRub * Got(line) / s.Bought : 0;
        int Rank(ConfirmLine line) => State(line) is not { } s ? 0 : Got(line) >= line.Count ? 3 : s.Fail switch { BuyFail.None => 1, BuyFail.Skipped => 2, _ => 0 };

        // Problems first once the run ends; moving tiles while buying would be hard to follow
        if (run is { Running: false })
        {
            lines = lines.OrderBy(Rank).ToList();
        }

        var ui = _ui;
        int total = lines.Sum(l => Units(l.Tpl, l.Count));
        int bought = lines.Sum(l => Got(l) >= l.Count ? Units(l.Tpl, l.Count) : MoneyTpls.Contains(l.Tpl) ? 0 : Got(l));
        string summary = run is null
            ? $"~{Short(lines.Sum(l => l.Rub))} ₽"
            : run.Running
            ? $"~{Short(lines.Sum(l => l.Rub))} ₽  ·  " + T($"{bought} of {total} bought", $"{bought} de {total} comprados")
            : $"{Short(lines.Sum(Paid))} ₽ {T("paid", "pago")}  ·  " + (bought >= total
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(Ui.Green)}>{T("All bought", "Tudo comprado")}</color>"
                : T($"{bought} of {total} bought", $"{bought} de {total} comprados"));
        var header = GUILayoutUtility.GetRect(width, 30);
        GUI.DrawTexture(new Rect(header.x, header.y + 6, 3, 18), ui.Tint(accent, 1));
        float tx = header.x + 12;
        if (group.Area is { } area)
        {
            IconCache.DrawSprite(new Rect(tx, header.y + 3, 24, 24), AreaIcons.Get(area));
            tx += 30;
        }

        GUI.Label(new Rect(tx, header.y, width, header.height),
            $"{title}   <color=#9a9a94>{total} {T("items", "itens")}  ·  {summary}</color>", ui.Header);
        GUILayout.Space(6);

        // Taller than the icon, name and price need, so the progress bar has its own strip
        const float tileW = 92, tileH = 122, gap = 8;
        int perRow = Math.Max(1, (int)((width + gap) / (tileW + gap)));
        int rows = (lines.Count + perRow - 1) / perRow;
        var grid = GUILayoutUtility.GetRect(width, rows * (tileH + gap));
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var tile = new Rect(grid.x + i % perRow * (tileW + gap), grid.y + i / perRow * (tileH + gap), tileW, tileH);
            bool hover = tile.Contains(Event.current.mousePosition);

            var state = State(line);
            int got = Got(line);
            bool done = state is not null && got >= line.Count;
            bool buying = state is { Buying: > 0 } && !done;
            bool failed = state is not null && !done && !buying && state.Fail != BuyFail.None;
            Color? status = done ? Ui.Green : buying ? Ui.Orange : failed ? state!.Fail switch { BuyFail.Price => Ui.Amber, BuyFail.Skipped => Ui.Grey, _ => Ui.Red } : null;

            ui.Box(tile, ui.Card, status is { } c && !done ? ui.Tint(c, 0.9f) : hover ? ui.Tint(accent, 0.8f) : ui.Line);
            // Only the contents fade: a faded card would let the hideout show through
            var color = GUI.color;
            if (done) GUI.color = new Color(1, 1, 1, 0.55f);
            DrawIcon(new Rect(tile.x + 8, tile.y + 8, tileW - 16, tileW - 16), line.Tpl);

            var badgeText = "x" + (MoneyTpls.Contains(line.Tpl) ? Short(line.Count) : line.Count.ToString());
            float badgeW = ui.Badge.CalcSize(new GUIContent(badgeText)).x + 10;
            var badge = new Rect(tile.xMax - badgeW - 4, tile.y + 4, badgeW, 16);
            GUI.DrawTexture(badge, ui.Tint(accent, 1));
            GUI.Label(badge, badgeText, ui.Badge);

            if (done || failed)
            {
                var mark = new Rect(tile.x + 4, tile.y + 4, 16, 16);
                GUI.DrawTexture(mark, ui.Tint(status!.Value, 1));
                GUI.Label(mark, done ? "✓" : state!.Fail == BuyFail.Skipped ? "-" : "!", ui.Badge);
            }

            GUI.Label(new Rect(tile.x + 4, tile.y + tileW - 4, tileW - 8, 16), Name(line.Tpl), ui.TileName);
            GUI.color = color;
            var price = ui.Styled(ui.TileName, status ?? Ui.Grey);
            GUI.Label(new Rect(tile.x + 4, tile.y + tileW + 10, tileW - 8, 14), TileStatus(line, state, got, done, buying, failed, run, Paid(line)), price);

            if (hover && state is not null && run is { Running: false } && !_busy && !done)
            {
                var icon = new Rect(tile.x + 8, tile.y + 8, tileW - 16, tileW - 16);
                bool canRetry = Buyable(line.Tpl);
                bool canSkip = state.Fail != BuyFail.Skipped && Retryable(state.Fail);
                if (canRetry || canSkip)
                {
                    GUI.DrawTexture(icon, ui.BlackTex);
                }

                if (canRetry && ui.FlatButton(new Rect(icon.x + 4, icon.y + 16, icon.width - 8, 20), T("RETRY", "TENTAR"), true))
                {
                    Buy(true, line.Tpl);
                }

                if (canSkip && ui.FlatButton(new Rect(icon.x + 4, icon.y + 42, icon.width - 8, 20), T("SKIP", "IGNORAR"), false))
                {
                    Skip(line.Tpl);
                }
            }

            if (state is not null)
            {
                var bar = new Rect(tile.x + 1, tile.yMax - 4, tileW - 2, 3);
                GUI.DrawTexture(bar, ui.Line);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * got / line.Count, bar.height), ui.Tint(status ?? Ui.Cream, 1));
            }

            if (hover)
            {
                _tooltip = $"<b>{ListName(line.Tpl)} x{line.Count}</b>\n{detailPrefix}: {line.Detail}\n{T("From", "De")}: {line.Source}\n~{line.Rub:N0} ₽";
                if (failed)
                {
                    _tooltip += $"\n<color=#{ColorUtility.ToHtmlStringRGB(status!.Value)}>{FailDetail(state!)}</color>";
                }
                else if (state is { Tries: > 1 })
                {
                    _tooltip += "\n" + T($"The first offer sold out. Bought on try {state.Tries} from the next one, {RetryChange(state)} per item.",
                        $"A primeira oferta esgotou. Comprado na tentativa {state.Tries} com a próxima, {RetryChange(state)} por item.");
                }
            }
        }

        GUILayout.Space(14);
    }

    private string TileStatus(ConfirmLine line, BuyState? state, int got, bool done, bool buying, bool failed, BuyRun? run, long paid)
    {
        string count = MoneyTpls.Contains(line.Tpl) ? $"{Short(got)}/{Short(line.Count)}" : $"{got}/{line.Count}";
        if (state is null) return Short(line.Rub) + " ₽";
        if (done && state.Tries > 1) return T($"retried · {RetryChange(state)}", $"refeito · {RetryChange(state)}");
        if (done) return T($"paid {Short(paid)} ₽", $"pago {Short(paid)} ₽");
        if (buying) return T($"buying {count}", $"comprando {count}");
        if (failed) return got > 0 ? $"{count} · {FailLabel(state)}" : FailLabel(state);
        if (run is { Running: true }) return got > 0 ? count : T("queued", "na fila");
        return got > 0 ? count : T("not bought", "não comprado");
    }

    // How far the replacement offer's price is from the first one, like "+4%"
    private static string RetryChange(BuyState state)
    {
        long change = state.RetryUnitRub * 100 / Math.Max(1, state.FirstUnitRub) - 100;
        return change >= 0 ? $"+{change}%" : $"{change}%";
    }

    private string FailLabel(BuyState state) => state.Fail switch
    {
        BuyFail.Gone => T("sold out", "esgotado"),
        BuyFail.Price => $"+{state.OfferUnitRub * 100 / Math.Max(1, state.MaxUnitRub) - 100}%",
        BuyFail.Money => T("no money", "sem dinheiro"),
        BuyFail.Space => T("no space", "sem espaço"),
        BuyFail.Limit => T("trader limit", "limite"),
        BuyFail.Skipped => T("skipped", "ignorado"),
        _ => T("failed", "falhou"),
    };

    private string FailDetail(BuyState state) => state.Fail switch
    {
        BuyFail.Gone => T("The offer sold out. Try again picks the next one.", "A oferta esgotou. Tentar de novo pega a próxima."),
        BuyFail.Price => T($"Next offer is {state.OfferUnitRub:N0} ₽ each, above your {_priceLimit.Value}% limit.", $"A próxima oferta custa {state.OfferUnitRub:N0} ₽ cada, acima do seu limite de {_priceLimit.Value}%."),
        BuyFail.Money => T("Not enough money.", "Dinheiro insuficiente."),
        BuyFail.Space => T("Not enough stash space.", "Sem espaço no estoque."),
        BuyFail.Limit => T("Trader buy limit reached.", "Limite de compra do comerciante atingido."),
        BuyFail.Skipped => T("Skipped, so Try again leaves it out.", "Ignorado, Tentar de novo deixa de fora."),
        _ => state.Error,
    };

    private string FailSummary(BuyFail fail) => fail switch
    {
        BuyFail.None => T("not bought", "não comprados"),
        BuyFail.Gone => T("sold out", "esgotados"),
        BuyFail.Price => T("above price limit", "acima do limite de preço"),
        BuyFail.Money => T("no money", "sem dinheiro"),
        BuyFail.Space => T("no space", "sem espaço"),
        BuyFail.Limit => T("trader limit", "limite do comerciante"),
        BuyFail.Skipped => T("skipped", "ignorados"),
        _ => T("failed", "falharam"),
    };

    private void DrawConfirmFooter(Rect r)
    {
        var ui = _ui;
        var run = _run;
        var left = new Rect(r.x, r.y + 38, 170, 46);
        var right = new Rect(r.x + 180, r.y + 38, r.width - 180, 46);
        if (run is not null)
        {
            long planned = run.Groups.SelectMany(g => g.Lines).Sum(l => l.Rub);
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 22),
                $"<color=#9a9a94>{T("Spent", "Gasto")}</color>   <b>{run.SpentRub:N0} ₽</b>   <color=#9a9a94>{T("of", "de")} ~{planned:N0} ₽</color>", ui.Header);
        }
        else if (_plan is { } plan)
        {
            var cost = string.Join("  +  ", plan.Cost.Select(c => $"{c.Value:N0} {Currency(c.Key)}"));
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 22), $"<color=#9a9a94>{T("Total", "Total")}</color>   <b>{cost}</b>", ui.Header);
        }

        if (run is { Running: true })
        {
            if (ui.FlatButton(left, _stop ? T("STOPPING", "PARANDO") : T("STOP", "PARAR"), false, !_stop))
            {
                _stop = true;
            }

            GUI.DrawTexture(right, ui.OrangeDim);
            float done = run.Planned == 0 ? 0 : (float)run.Bought / run.Planned;
            GUI.DrawTexture(new Rect(right.x, right.y, right.width * done, right.height), ui.Tint(Ui.Orange, 0.45f));
            var text = ui.Styled(ui.ButtonText, Ui.Cream, 16);
            GUI.Label(right, T($"BUYING {run.Bought} OF {run.Planned}", $"COMPRANDO {run.Bought} DE {run.Planned}"), text);
            return;
        }

        if (ui.FlatButton(left, T("BACK", "VOLTAR"), false, !_busy || run is not null))
        {
            _confirming = false;
            _run = null;
            _scroll = Vector2.zero;
        }

        if (run is null)
        {
            if (ui.FlatButton(right, _busy ? _status : T("CONFIRM PURCHASE", "CONFIRMAR COMPRA"), true, !_busy && _plan?.Operations.Count > 0))
            {
                Buy();
            }

            if (!_busy && _status.Length > 0)
            {
                GUI.Label(new Rect(r.x, r.y + 8, r.width, 22), _status, ui.Styled(ui.Muted, align: TextAnchor.MiddleRight));
            }

            return;
        }

        var retry = RetryTpls();
        if (retry.Count > 0)
        {
            if (ui.FlatButton(right, T($"TRY AGAIN ({retry.Count})", $"TENTAR DE NOVO ({retry.Count})"), true, !_busy))
            {
                Buy(true);
            }
        }
        else if (ui.FlatButton(right, T("DONE", "CONCLUÍDO"), true))
        {
            _confirming = false;
            _run = null;
            _scroll = Vector2.zero;
        }

        GUI.Label(new Rect(r.x, r.y + 86, r.width, 16), _status.Length > 0 ? _status : RunSummary(run, retry), ui.Muted);
    }

    private string RunSummary(BuyRun run, List<string> retry)
    {
        var parts = new List<string> { T($"{run.Bought} of {run.Planned} bought", $"{run.Bought} de {run.Planned} comprados") };
        foreach (var group in run.Items.Where(s => s.Value.Bought < s.Value.Planned).GroupBy(s => s.Value.Fail).OrderBy(g => g.Key))
        {
            parts.Add($"{group.Sum(s => MoneyTpls.Contains(s.Key) ? 1 : s.Value.Planned - s.Value.Bought)} {FailSummary(group.Key)}");
        }

        if (retry.Count > 0 && _plan is { } plan)
        {
            var tpls = new HashSet<string>(retry);
            parts.Add(T("try again", "tentar de novo") + $" ~{Short(plan.Purchases.Where(p => tpls.Contains(p.Tpl)).Sum(p => p.Rub))} ₽");
        }

        return string.Join("  ·  ", parts);
    }

    private static string Short(long value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000d).ToString("0.##") + "M",
        >= 1_000 => (value / 1000d).ToString("0.#") + "k",
        _ => value.ToString("N0"),
    };
}
