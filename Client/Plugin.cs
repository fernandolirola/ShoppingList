using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using SPT.Reflection.Utils;
using UnityEngine;

namespace ShoppingList.Client;

[BepInPlugin("com.zezinho.shoppinglist", "zezinho-ShoppingList", "1.1.1")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    private static readonly AccessTools.FieldRef<AreaScreenSubstrate, AreaData> DataToShow = AccessTools.FieldRefAccess<AreaScreenSubstrate, AreaData>("_dataToShow");

    private ConfigEntry<bool> _portuguese = null!;
    private ConfigEntry<bool> _crafts = null!;
    private ConfigEntry<KeyboardShortcut> _hotkey = null!;
    private ConfigEntry<bool> _future = null!;
    private ConfigEntry<SortMode> _sort = null!;

    private AreaScreenSubstrate? _areaScreen;
    private HideoutScreenOverlay? _overlay;
    private float _nextLookup;
    private float _lastToggle;
    private readonly ToolbarButton _toolbar = new();

    private bool _open;
    private int? _focusArea;
    private HashSet<int>? _selected;
    private List<AreaPlan>? _allAreas;
    private int _requests;
    private PlanResponse? _plan;
    private PlanResponse? _raw;
    private readonly HashSet<string> _skipped = new();
    private bool _busy;
    private string _status = "";
    private Rect _window = new(0, 0, W, H);
    private Vector2 _scroll;
    private Ui _ui = null!;

    private void Awake()
    {
        Log = Logger;
        _portuguese = Config.Bind("General", "Portuguese", false, "Show the panel in Portuguese");
        _crafts = Config.Bind("General", "Buy craft ingredients", true, "For found-in-raid items with an unlocked craft, buy that craft's ingredients");
        _hotkey = Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.L, KeyCode.LeftControl), "Opens the shopping list in the hideout, or in raid while the inventory is open");
        _future = Config.Bind("General", "Show future upgrades", false, "Also list the levels after the next one");
        _sort = Config.Bind("General", "Sort by", SortMode.Default, "How the areas in the list are sorted");
        new Harmony("com.zezinho.shoppinglist").PatchAll(typeof(ScrollPatch));
    }

    private bool PT => _portuguese.Value;
    private string T(string en, string pt) => PT ? pt : en;

    private void Update()
    {
        if (Time.unscaledTime >= _nextLookup)
        {
            _nextLookup = Time.unscaledTime + 1f;
            if (_areaScreen == null) _areaScreen = FindObjectOfType<AreaScreenSubstrate>(true);
            if (_overlay == null) _overlay = FindObjectOfType<HideoutScreenOverlay>(true);
        }

        if (!Active && _open)
        {
            _open = false;
        }

        if (_areaScreen != null)
        {
            var area = OpenArea;
            bool canUpgrade = area != null && area.NextStage != null && area.Status != EAreaStatus.Upgrading;
            _areaIcon ??= ToolbarButton.CartSprite();
            _areaButton.Sync(_areaScreen, canUpgrade, T("Shopping list", "Lista de compras"), _areaIcon, () =>
            {
                if (OpenArea is { } open) Toggle((int)open.Template.Type);
            });
        }

        if (_overlay != null)
        {
            _toolbar.Sync(_overlay, () => T("Shopping list", "Lista de compras"), () => Toggle(null));
        }

        if (Active && _hotkey.Value.IsDown())
        {
            Toggle(null);
        }

        // Buying by hand or donating changes the stash or the hideout without going through the list
        // In raid only while the list is on screen, so a closed inventory costs nothing during fights
        if (_open && Shown && !_busy && Time.unscaledTime >= _nextSignature)
        {
            _nextSignature = Time.unscaledTime + (InRaid ? 3f : 1.5f);
            long signature = StashSignature();
            if (_signatureKnown && signature != _signature)
            {
                Refresh();
            }

            _signature = signature;
            _signatureKnown = true;
        }

        ScrollPatch.Blocked = _open && InHideout;
        _blocker.Sync(_open && Shown, new Rect(_window.x * Ui.Scale, _window.y * Ui.Scale, _window.width * Ui.Scale, _window.height * Ui.Scale));
    }

    private long _signature;
    private bool _signatureKnown;
    private float _nextSignature;

    private long StashSignature()
    {
        long sum = 0;
        if (RaidPlayer is { } player)
        {
            // Only items the list asks for, or every shot would change the magazine count and re-plan
            var wanted = WantedTpls();
            foreach (var item in player.Inventory.Equipment.GetAllItems())
            {
                if (!wanted.Contains(item.TemplateId.ToString())) continue;
                sum = sum * 31 + item.StackObjectsCount + item.TemplateId.GetHashCode() + (item.SpawnedInSession ? 7 : 0);
            }

            return sum;
        }

        if (ClientAppUtils.GetClientApp()?.Session is ClientBackendSession session && session.Profile?.Inventory?.Stash is { } stash)
        {
            foreach (var item in stash.GetAllItems())
            {
                sum = sum * 31 + item.StackObjectsCount + item.TemplateId.GetHashCode();
            }
        }

        if (Singleton<HideoutRepresentation>.Instance is { } hideout)
        {
            foreach (var area in hideout.AreaDatas)
            {
                sum = sum * 31 + area.CurrentLevel * 16 + (int)area.Status;
            }
        }

        return sum;
    }

    private HashSet<string> _wanted = new();
    private PlanResponse? _wantedFrom;

    private HashSet<string> WantedTpls()
    {
        if (_raw != _wantedFrom)
        {
            _wantedFrom = _raw;
            _wanted = _raw is null
                ? new HashSet<string>()
                : new HashSet<string>(_raw.Areas.Concat(_raw.Future).SelectMany(a => a.Items).Concat(_raw.Crafts.SelectMany(c => c.Ingredients)).Select(n => n.Tpl)
                    .Concat(_raw.Crafts.Select(c => c.Tpl)));
        }

        return _wanted;
    }

    private readonly AreaButton _areaButton = new();
    private readonly ClickBlocker _blocker = new();
    private Sprite? _areaIcon;

    private bool InHideout => _overlay != null && _overlay.isActiveAndEnabled;

    private static Player? RaidPlayer => Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance is { } world && world is not HideoutGameWorld ? world.MainPlayer : null;

    private static bool InRaid => RaidPlayer != null;

    private bool Active => InHideout || InRaid;

    // In raid the list only shows with the cursor out, so clicks never reach the weapon
    private bool Shown => InHideout || InRaid && Cursor.visible;

    private AreaData? OpenArea => _areaScreen != null && _areaScreen.isActiveAndEnabled ? DataToShow(_areaScreen) : null;

    private void Toggle(int? area)
    {
        if (_open && _focusArea == area)
        {
            // The game can hand over the same click twice; only that echo may not close a list it just opened
            if (Time.unscaledTime - _lastToggle >= 0.25f)
            {
                _open = false;
            }

            return;
        }

        _lastToggle = Time.unscaledTime;
        _open = true;
        _focusArea = area;
        _status = "";
        _confirming = false;
        _scroll = Vector2.zero;
        _window.x = (Screen.width / Ui.Scale - _window.width) / 2;
        _window.y = (Screen.height / Ui.Scale - _window.height) / 2;
        Refresh();
    }

    private async void Refresh()
    {
        _busy = true;
        int request = ++_requests;
        try
        {
            var plan = await RequestPlan();
            // Quick ticking sends several requests; only the newest one may land
            if (request != _requests)
            {
                return;
            }

            _raw = plan;
            ApplySkips();
            if (_plan!.Error != null)
            {
                _status = _plan.Error;
            }
            else if (_focusArea is null && _selected is null)
            {
                _selected = new HashSet<int>(_plan.Areas.Select(a => a.Type));
                _allAreas = _plan.Areas;
            }
        }
        catch (Exception ex)
        {
            _status = T("Could not reach the server", "Sem resposta do servidor");
            Log.LogError(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    // The server sends one operation per purchase, in the same order, so unticked items are dropped pairwise
    private void ApplySkips()
    {
        if (_raw is null)
        {
            _plan = null;
            return;
        }

        var plan = new PlanResponse { Error = _raw.Error, Areas = _raw.Areas, Crafts = _raw.Crafts, Future = _raw.Future, Unavailable = _raw.Unavailable, Money = _raw.Money, FleaUnlocked = _raw.FleaUnlocked };
        for (int i = 0; i < _raw.Purchases.Count && i < _raw.Operations.Count; i++)
        {
            var purchase = _raw.Purchases[i];
            if (_skipped.Contains(purchase.Tpl))
            {
                continue;
            }

            plan.Purchases.Add(purchase);
            plan.Operations.Add(_raw.Operations[i]);
            plan.Cost[purchase.Currency] = plan.Cost.TryGetValue(purchase.Currency, out var sum) ? sum + purchase.Price : purchase.Price;
            plan.TotalRub += purchase.Rub;
        }

        _plan = plan;
    }

    private async Task<PlanResponse> RequestPlan()
    {
        var areas = _focusArea is { } one ? new List<int> { one } : _selected?.ToList() ?? new List<int>();
        if (_focusArea is null && _selected is { Count: 0 })
        {
            return new PlanResponse();
        }

        Dictionary<string, int>? carried = null, carriedFir = null;
        if (RaidPlayer is { } player)
        {
            carried = new Dictionary<string, int>();
            carriedFir = new Dictionary<string, int>();
            foreach (var item in player.Inventory.Equipment.GetAllItems())
            {
                string tpl = item.TemplateId.ToString();
                carried[tpl] = carried.TryGetValue(tpl, out var any) ? any + item.StackObjectsCount : item.StackObjectsCount;
                if (item.SpawnedInSession)
                {
                    carriedFir[tpl] = carriedFir.TryGetValue(tpl, out var fir) ? fir + item.StackObjectsCount : item.StackObjectsCount;
                }
            }
        }

        var body = JsonConvert.SerializeObject(new { areas, crafts = _crafts.Value, future = _future.Value, overview = _focusArea is null, carried, carriedFir, raid = carried != null });
        var json = await RequestHandler.PostJsonAsync("/shoppinglist/plan", body);
        return JsonConvert.DeserializeObject<PlanResponse>(json) ?? new PlanResponse();
    }

    private async void Buy()
    {
        if (InRaid || ClientAppUtils.GetClientApp()?.Session is not ClientBackendSession session || _plan is null)
        {
            return;
        }

        _busy = true;
        int bought = 0, failed = 0;
        string? lastError = null;
        try
        {
            // Offers can sell out between planning and buying, so leftovers get a fresh plan
            for (int round = 0; round < 3 && _plan.Operations.Count > 0; round++)
            {
                int roundFailed = 0;
                foreach (var op in _plan.Operations)
                {
                    _status = T($"Buying... {bought + 1}", $"Comprando... {bought + 1}");
                    var result = await Send(session, op);
                    if (result.Succeed)
                    {
                        bought++;
                    }
                    else
                    {
                        roundFailed++;
                        lastError = result.Error;
                        if (result.Error?.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            round = 3;
                            break;
                        }
                    }
                }

                failed += roundFailed;
                _raw = await RequestPlan();
                ApplySkips();
                if (roundFailed == 0)
                {
                    break;
                }
            }

            _status = lastError is null
                ? T($"Done: {bought} purchase{(bought == 1 ? "" : "s")}", $"Pronto: {bought} compra{(bought == 1 ? "" : "s")}")
                : T($"{bought} purchases, last error: {lastError}", $"{bought} compras, Ãºltimo erro: {lastError}");
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Log.LogError(ex);
        }
        finally
        {
            _busy = false;
            _signatureKnown = false;
            // Back to the list, which was re-planned after the purchases, so the progress shows
            _confirming = false;
            _scroll = Vector2.zero;
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

    private void OnGUI()
    {
        if (!Shown)
        {
            return;
        }

        _ui ??= new Ui();
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Ui.Scale, Ui.Scale, 1));

        // Fallback buttons, only drawn when the game's own buttons could not be copied
        var area = OpenArea;
        if (!InHideout)
        {
            // In raid only the hotkey opens it
        }
        else if (area != null && area.NextStage != null && area.Status != EAreaStatus.Upgrading)
        {
            var rect = new Rect(Screen.width / Ui.Scale / 2 + 130, 64, 190, 30);
            if (!_areaButton.Exists && _ui.FlatButton(rect, T("Shopping list", "Lista de compras"), true))
            {
                Toggle((int)area.Template.Type);
            }
        }
        else if (!_toolbar.Exists && _ui.FlatButton(new Rect(14, 420, 140, 30), T("Shopping list", "Lista de compras"), true))
        {
            Toggle(null);
        }

        if (_open)
        {
            _window = GUI.Window(0x5A5A, _window, DrawWindow, GUIContent.none, _ui.Plain);
        }

        GUI.matrix = matrix;
    }

    private static string Name(string tpl) => (tpl + " ShortName").Localized();

    private static string Currency(string tpl) => tpl switch
    {
        "5449016a4bdc2d6f028b456f" => "â‚½",
        "5696686a4bdc2da3298b456a" => "$",
        "569668774bdc2da2298b4568" => "â‚¬",
        _ => Name(tpl),
    };
}
