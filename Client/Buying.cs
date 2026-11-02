using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using Newtonsoft.Json.Linq;
using SPT.Reflection.Utils;

namespace ShoppingList.Client;

public partial class Plugin
{
    private enum BuyFail { None, Gone, Price, Money, Space, Limit, Other, Skipped }

    private sealed class BuyState
    {
        public int Planned;
        public int Bought;
        public long SpentRub;
        public int Buying;
        public long MaxUnitRub;
        public long OfferUnitRub;
        public BuyFail Fail;
        public string Error = "";
        // Set when an automatic retry bought it from a replacement offer
        public int Tries = 1;
        public long FirstUnitRub;
        public long RetryUnitRub;
    }

    // What the review screen showed when buying started, so tiles stay put while re-plans shrink the list
    private sealed class BuyRun
    {
        public List<ConfirmGroup> Groups = new();
        public readonly Dictionary<string, BuyState> Items = new();
        public long SpentRub;
        public bool Running;
        // Set when a single tile's Retry started the run, so other unfinished items are left alone
        public HashSet<string>? Only;
        // A currency counts as one item, or a few thousand dollars would swamp the progress bar
        public int Planned => Items.Sum(s => Units(s.Key, s.Value.Planned));
        public int Bought => Items.Sum(s => MoneyTpls.Contains(s.Key) ? (s.Value.Bought >= s.Value.Planned ? 1 : 0) : Math.Min(s.Value.Bought, s.Value.Planned));
    }

    private BuyRun? _run;
    private bool _stop;

    private static long UnitRub(Purchase p) => (p.Rub + Math.Max(1, p.Count) - 1) / Math.Max(1, p.Count);

    private static bool Retryable(BuyFail fail) => fail is BuyFail.None or BuyFail.Gone or BuyFail.Price or BuyFail.Other;

    private static BuyFail Classify(string? error)
    {
        bool Has(string s) => error?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
        if (Has("space")) return BuyFail.Space;
        if (Has("money")) return BuyFail.Money;
        if (Has("limit")) return BuyFail.Limit;
        if (Has("offer") || Has("exist") || Has("not found") || Has("stock")) return BuyFail.Gone;
        return BuyFail.Other;
    }

    private BuyRun NewRun(PlanResponse plan)
    {
        var run = new BuyRun { Groups = BuildConfirm(plan) };
        foreach (var group in plan.Purchases.GroupBy(p => p.Tpl))
        {
            run.Items[group.Key] = new BuyState { Planned = group.Sum(p => p.Count), MaxUnitRub = group.Max(UnitRub), FirstUnitRub = group.Max(UnitRub) };
        }

        return run;
    }

    // Items of the run that the current plan can still buy, with the operations to do it
    private List<(Purchase purchase, JObject op)> Pending(BuyRun run, PlanResponse plan)
    {
        var pending = new List<(Purchase, JObject)>();
        for (int i = 0; i < plan.Purchases.Count && i < plan.Operations.Count; i++)
        {
            var purchase = plan.Purchases[i];
            if (run.Items.TryGetValue(purchase.Tpl, out var state) && state.Bought < state.Planned && Retryable(state.Fail) && run.Only?.Contains(purchase.Tpl) != false)
            {
                pending.Add((purchase, plan.Operations[i]));
            }
        }

        return pending;
    }

    private List<string> RetryTpls()
    {
        if (_run is not { Running: false } run || _plan is null)
        {
            return new List<string>();
        }

        var buyable = new HashSet<string>(_plan.Purchases.Select(p => p.Tpl));
        return run.Items.Where(s => s.Value.Bought < s.Value.Planned && Retryable(s.Value.Fail) && buyable.Contains(s.Key)).Select(s => s.Key).ToList();
    }

    private bool Buyable(string tpl) => _plan?.Purchases.Any(p => p.Tpl == tpl) == true;

    private void Skip(string tpl)
    {
        if (_run?.Items.TryGetValue(tpl, out var state) == true)
        {
            state.Fail = BuyFail.Skipped;
        }
    }

    private async void Buy(bool retry = false, string? only = null)
    {
        if (InRaid || ClientAppUtils.GetClientApp()?.Session is not ClientBackendSession session || _plan is null)
        {
            return;
        }

        if (!retry || _run is null)
        {
            _run = NewRun(_plan);
        }
        else
        {
            // A single tile's Retry also takes items that hit money, space or trader limits, which may be fixed by now
            var tpls = only is null ? RetryTpls() : Buyable(only) ? new List<string> { only } : new List<string>();
            _run.Only = only is null ? null : new HashSet<string>(tpls);
            // The retry button showed the new total, so the current offers are the approved prices
            foreach (var tpl in tpls)
            {
                var state = _run.Items[tpl];
                state.Fail = BuyFail.None;
                state.Error = "";
                state.MaxUnitRub = Math.Max(state.MaxUnitRub, _plan.Purchases.Where(p => p.Tpl == tpl).Max(UnitRub));
            }
        }

        var run = _run;
        run.Running = true;
        _busy = true;
        _stop = false;
        _status = "";
        try
        {
            // Offers can sell out between planning and buying, so leftovers get a fresh plan
            for (int round = 0; round < (_autoRetry.Value ? 3 : 1) && !_stop; round++)
            {
                var pending = Pending(run, _plan!);
                if (pending.Count == 0)
                {
                    break;
                }

                int gone = 0;
                foreach (var (purchase, op) in pending)
                {
                    var state = run.Items[purchase.Tpl];
                    if (_stop) break;
                    // A trader limit hit earlier in this round
                    if (!Retryable(state.Fail)) continue;

                    state.OfferUnitRub = UnitRub(purchase);
                    if (state.OfferUnitRub * 100 > state.MaxUnitRub * (100 + _priceLimit.Value))
                    {
                        state.Fail = BuyFail.Price;
                        continue;
                    }

                    state.Buying = purchase.Count;
                    var result = await Send(session, op);
                    state.Buying = 0;
                    if (result.Succeed)
                    {
                        state.Bought += purchase.Count;
                        state.SpentRub += purchase.Rub;
                        if (round > 0)
                        {
                            state.Tries = Math.Max(state.Tries, round + 1);
                            state.RetryUnitRub = Math.Max(state.RetryUnitRub, UnitRub(purchase));
                        }

                        run.SpentRub += purchase.Rub;
                        if (state.Bought >= state.Planned) state.Fail = BuyFail.None;
                        continue;
                    }

                    state.Fail = Classify(result.Error);
                    state.Error = result.Error ?? "";
                    if (state.Fail is BuyFail.Space or BuyFail.Money)
                    {
                        // Every later purchase would fail the same way
                        _stop = true;
                    }
                    else if (state.Fail != BuyFail.Limit)
                    {
                        gone++;
                    }
                }

                _raw = await RequestPlan();
                ApplySkips();
                if (gone == 0)
                {
                    break;
                }
            }

            var noOffers = new HashSet<string>((_raw?.Unavailable ?? new List<Need>()).Select(n => n.Tpl));
            var buyable = new HashSet<string>(_plan!.Purchases.Select(p => p.Tpl));
            foreach (var pair in run.Items.Where(s => s.Value.Bought < s.Value.Planned && s.Value.Fail == BuyFail.None && !buyable.Contains(s.Key) && noOffers.Contains(s.Key)))
            {
                pair.Value.Fail = BuyFail.Gone;
            }
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Log.LogError(ex);
        }
        finally
        {
            foreach (var state in run.Items.Values) state.Buying = 0;
            run.Running = false;
            _busy = false;
            _stop = false;
            _signatureKnown = false;
        }
    }

    private static async Task<IResult> Send(ClientBackendSession session, JObject op)
    {
        // Continuing inside the game's callback would queue the next purchase before the game finishes
        // handling this one, and a warning there clears its queue, losing that purchase
        var source = new TaskCompletionSource<IResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SendOperationRightNow(op, result => source.TrySetResult(result));
        var finished = await Task.WhenAny(source.Task, Task.Delay(20000));
        await Task.Yield();
        return finished == source.Task ? source.Task.Result : new FailedResult("Timed out");
    }

    private sealed class FailedResult(string error) : IResult
    {
        public string Error => error;
        public bool Succeed => false;
        public bool Failed => true;
        public int ErrorCode => 0;
    }
}
