using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace ShoppingList;

// BetterFlea makes most of an item's flea offers only once it is searched, so planned items are searched first
[Injectable(InjectionType.Singleton)]
public sealed class BetterFleaFill
{
    // BetterFlea skips bigger searches and keeps only its last few searched lists
    private const int MaxPerCall = 400;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private bool _resolved;
    private PropertyInfo? _enabled;
    private MethodInfo? _fillUp;
    private IDictionary? _filling;

    public void Fill(IEnumerable<MongoId> tpls)
    {
        try
        {
            Resolve();
            if (_fillUp is null || _enabled?.GetValue(null) is not true)
            {
                return;
            }

            foreach (var chunk in tpls.Distinct().Chunk(MaxPerCall))
            {
                var set = new HashSet<MongoId>(chunk);
                _fillUp.Invoke(null, [set]);

                var timer = Stopwatch.StartNew();
                while (_filling is not null && set.Any(t => _filling.Contains(t)) && timer.Elapsed < Timeout)
                {
                    Thread.Sleep(25);
                }
            }
        }
        catch
        {
            // BetterFlea changed shape; the plan just sees fewer flea offers
        }
    }

    private void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        var type = AccessTools.TypeByName("BetterFlea.Offers.OnDemandOffers");
        if (type is null)
        {
            return;
        }

        _enabled = AccessTools.Property(type, "Enabled");
        _fillUp = AccessTools.Method(type, "FillUp", [typeof(HashSet<MongoId>)]);
        _filling = AccessTools.Field(type, "Filling")?.GetValue(null) as IDictionary;
    }
}
