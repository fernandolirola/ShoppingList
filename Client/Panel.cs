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
            bool crafts = ui.Checkbox(new Rect(Pad, top, 24, 22), _crafts.Value);
            GUI.Label(new Rect(Pad + 24, top, 520, 22), T("Buy ingredients to craft found-in-raid items you can make", "Comprar ingredientes para craftar itens FIR que você já pode fazer"), ui.Text);
            if (crafts != _crafts.Value)
            {
                _crafts.Value = crafts;
                Refresh();
            }

            top += 32;
            DrawOptions(top);
            top += 32;
        }

        float footer = _confirming ? 112 : 96;
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

        // Refresh, language switch and close
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
        int items = plan?.Purchases.Sum(p => p.Count) ?? 0;
        string cost = plan is null || plan.Cost.Count == 0 ? "0 ₽" : string.Join(" + ", plan.Cost.Select(c => $"{Short(c.Value)} {Currency(c.Key)}"));
        bool enough = plan is null || plan.Cost.All(c => plan.Money.TryGetValue(c.Key, out var have) && have >= c.Value);
        string money = plan is null ? "-" : Short(plan.Money.TryGetValue(MoneyTpls.First(), out var rub) ? rub : 0) + " ₽";

        Chip(new Rect(r.x, r.y, w, r.height), T("ITEMS TO BUY", "ITENS A COMPRAR"), items.ToString(), Ui.Cream);
        Chip(new Rect(r.x + w + gap, r.y, w, r.height), T("TOTAL COST", "CUSTO TOTAL"), cost, Ui.Orange);
        Chip(new Rect(r.x + (w + gap) * 2, r.y, w, r.height), T("YOUR ROUBLES", "SEUS RUBLOS"), money, enough ? Ui.Green : Ui.Red);

        void Chip(Rect c, string label, string value, Color color)
        {
            ui.Box(c, ui.Card, ui.Line);
            GUI.DrawTexture(new Rect(c.x, c.y, 2, c.height), ui.Tint(color, 0.8f));
            GUI.Label(new Rect(c.x + 14, c.y + 9, c.width - 20, 14), label, ui.ChipLabel);
            var style = new GUIStyle(ui.ChipValue);
            style.normal.textColor = color;
            GUI.Label(new Rect(c.x + 14, c.y + 25, c.width - 20, 24), value, style);
        }
    }

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

    // Draws the open/closed arrow at x and makes the rest of the header a toggle; returns whether it is closed
    private bool Collapsible(Rect header, ref float x, string key)
    {
        bool collapsed = Collapsible(new Rect(x, header.y, header.xMax - x, header.height), x, key);
        x += 18;
        return collapsed;
    }

    private bool Collapsible(Rect hit, float arrowX, string key)
    {
        bool collapsed = _collapsed.Contains(key);
        if (hit.Contains(Event.current.mousePosition)) GUI.DrawTexture(hit, _ui.Hover);
        GUI.Label(new Rect(arrowX, hit.y, 14, hit.height), collapsed ? "►" : "▼", _ui.Chevron);
        if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
        {
            if (!_collapsed.Remove(key)) _collapsed.Add(key);
        }

        return collapsed;
    }

    private bool _sortDesc = true;

    private void DrawOptions(float top)
    {
        var ui = _ui;
        GUI.Label(new Rect(Pad, top, 60, 24), T("Sort by", "Ordenar"), ui.Muted);
        float x = Pad + 60;
        foreach (var (mode, label) in new[] { (SortMode.Default, T("Default", "Padrão")), (SortMode.Progress, T("Progress %", "Progresso %")), (SortMode.Rarity, T("Rarity", "Raridade")) })
        {
            bool active = _sort.Value == mode;
            string text = active && mode != SortMode.Default ? label + (_sortDesc ? "  ▼" : "  ▲") : label;
            if (ui.FlatButton(new Rect(x, top, 112, 24), text, active))
            {
                if (active) _sortDesc = !_sortDesc; else { _sort.Value = mode; _sortDesc = true; }
            }

            x += 118;
        }

        float fx = W - Pad - 250;
        bool future = ui.Checkbox(new Rect(fx, top + 1, 24, 22), _future.Value);
        GUI.Label(new Rect(fx + 24, top + 1, 226, 22), T("Show future upgrades", "Mostrar melhorias futuras"), ui.Text);
        if (future != _future.Value)
        {
            _future.Value = future;
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
        if (_sort.Value == SortMode.Default) return list.OrderByDescending(on).ToList();
        Func<AreaPlan, float> key = _sort.Value == SortMode.Progress ? Progress : a => RarityKey(a);
        var ordered = list.OrderByDescending(on);
        return (_sortDesc ? ordered.ThenByDescending(key) : ordered.ThenBy(key)).ToList();
    }

    private List<Need> Rows(AreaPlan area) => _sort.Value != SortMode.Rarity
        ? area.Items
        : area.Items.OrderByDescending(i => i.Missing > 0).ThenBy(i => _sortDesc ? -i.Rarity : i.Rarity).ToList();

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
        foreach (var area in Ordered(areas, a => !overview || _selected!.Contains(a.Type)))
        {
            DrawArea(area, width, overview, "a" + area.Type);
        }

        if (_future.Value && plan.Future.Count > 0)
        {
            var header = GUILayoutUtility.GetRect(width, 38);
            ui.Box(header, ui.Card, ui.Line);
            GUI.DrawTexture(new Rect(header.x, header.y, 2, header.height), ui.Tint(Ui.Grey, 1));
            float hx = header.x + 12;
            bool futureCollapsed = Collapsible(header, ref hx, "future");
            GUI.Label(new Rect(hx, header.y, width, header.height), $"{T("FUTURE UPGRADES", "MELHORIAS FUTURAS")}   <color=#9a9a94>{T("nothing is bought for these", "nada é comprado para elas")}</color>", ui.Header);
            GUILayout.Space(6);
            foreach (var area in futureCollapsed ? [] : Ordered(plan.Future, _ => true))
            {
                DrawArea(area, width, false, $"f{area.Type}-{area.Level}");
            }

            GUILayout.Space(6);
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
                bool craftCollapsed = Collapsible(sub, ref cx, "c" + craft.Tpl);
                DrawIcon(new Rect(cx, sub.y + 3, RowH - 6, RowH - 6), craft.Tpl);
                GUI.Label(new Rect(cx + RowH + 6, sub.y, width - 220, sub.height),
                    $"<b>{Name(craft.Tpl)} x{craft.Missing}</b>   <color=#9a9a94>{((EAreaType)craft.Area).LocalizeAreaName()}  ·  {craft.Runs} {(craft.Runs == 1 ? T("run", "vez") : T("runs", "vezes"))}</color>", ui.Text);
                int canRun = CraftableRuns(craft);
                var (craftLabel, craftColor) = canRun >= craft.Runs
                    ? (T($"CAN CRAFT x{craft.Runs}", $"PODE CRAFTAR x{craft.Runs}"), Ui.Green)
                    : canRun > 0
                        ? (T($"CAN CRAFT {canRun} OF {craft.Runs}", $"PODE CRAFTAR {canRun} DE {craft.Runs}"), Ui.Orange)
                        : (T("BUY INGREDIENTS", "COMPRAR INGREDIENTES"), Ui.Grey);
                ui.PillLabel(new Rect(sub.xMax - 144, sub.y + 10, 136, 20), craftLabel, craftColor);
                for (int i = 0; !craftCollapsed && i < craft.Ingredients.Count; i++)
                {
                    DrawNeedRow(craft.Ingredients[i], width, CraftIndent, i % 2 == 1);
                }
            }

            GUILayout.Space(12);
        }

        if (!plan.FleaUnlocked)
        {
            GUILayout.Label(T("Flea market is locked for your level, only traders are used.", "Mercado bloqueado no seu nível, só comerciantes."), ui.Muted);
        }
    }

    private void DrawArea(AreaPlan area, float width, bool overview, string key)
    {
        var ui = _ui;
        bool on = !overview || _selected!.Contains(area.Type);
        var header = GUILayoutUtility.GetRect(width, 38);
        ui.Box(header, on ? ui.Card : ui.Clear, ui.Line);
        float x = header.x + 12;
        if (overview)
        {
            if (ui.Checkbox(new Rect(x, header.y, 24, header.height), on) != on)
            {
                if (on) _selected!.Remove(area.Type); else _selected!.Add(area.Type);
                Refresh();
            }

            x += 26;
        }

        bool collapsed = Collapsible(header, ref x, key);
        string name = ((EAreaType)area.Type).LocalizeAreaName();
        var nameStyle = on ? ui.Header : new GUIStyle(ui.Header) { normal = { textColor = Ui.Grey } };
        float nameWidth = nameStyle.CalcSize(new GUIContent(name)).x;
        GUI.Label(new Rect(x, header.y, nameWidth, header.height), name, nameStyle);
        ui.PillLabel(new Rect(x + nameWidth + 10, header.y + 10, 52, 18), $"{T("LVL", "NÍV")} {area.Level}", on ? Ui.Orange : Ui.Grey);
        int ready = area.Items.Count(i => i.Missing == 0);
        float progress = Progress(area);
        var bar = new Rect(header.xMax - 112, header.y + 17, 96, 4);
        GUI.DrawTexture(bar, ui.Line);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), ready == area.Items.Count ? ui.Tint(Ui.Green, 1) : ui.OrangeTex);
        var count = new GUIStyle(ui.Muted) { alignment = TextAnchor.MiddleRight };
        GUI.Label(new Rect(bar.x - 160, header.y, 152, header.height), $"{ready}/{area.Items.Count} {T("ready", "prontos")}  ·  {Mathf.FloorToInt(progress * 100)}%", count);
        if (collapsed)
        {
            GUILayout.Space(6);
            return;
        }

        var rows = Rows(area);
        for (int i = 0; i < rows.Count; i++)
        {
            DrawNeedRow(rows[i], width, 0, i % 2 == 1, on);
        }

        GUILayout.Space(12);
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

        string rarity = need.Missing == 0 ? "" : need.Rarity switch
        {
            2 => $"  <color=#C792EA>{T("very rare", "muito raro")}</color>",
            1 => $"  <color=#8FB8E8>{T("rare", "raro")}</color>",
            0 => $"  <color=#9a9a94>{T("common", "comum")}</color>",
            _ => "",
        };
        string tags = (need.Fir ? "  <color=#F2AA3C><b>FIR</b></color>" : "") + (need.Tool ? $"  <color=#9a9a94>{T("tool", "ferramenta")}</color>" : "") + rarity;
        GUI.Label(new Rect(x, r.y, 300 - indent, r.height), Name(need.Tpl) + tags, ui.Text);

        bool money = need.Plan == "money";
        int have = need.Have + need.Donated;
        string haveColor = need.Missing == 0 ? "#78CC6B" : "#EFEBE0";
        string amount = money ? $"{Short(need.Required)}" : $"<color={haveColor}><b>{have}</b></color><color=#9a9a94> / {need.Required}</color>";
        GUI.Label(new Rect(r.x + 380, r.y, 110, r.height), amount, ui.Text);
        if (need.Carried > 0)
        {
            var carried = new GUIStyle(ui.Muted);
            carried.normal.textColor = Ui.Green;
            GUI.Label(new Rect(r.x + 440, r.y, 86, r.height), $"{need.Carried} {T("on you", "com você")}", carried);
        }
        else if (need.Donated > 0)
        {
            GUI.Label(new Rect(r.x + 440, r.y, 80, r.height), $"{need.Donated} {T("donated", "doado")}", ui.Muted);
        }

        bool buyable = purchasing && need.Plan is ("buy" or "partial");
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
            "money" => need.Missing > 0 ? (T("NEED MONEY", "FALTA DINHEIRO"), Ui.Red) : (T("READY", "PRONTO"), Ui.Green),
            "unavailable" => (T("NOT FOR SALE", "SEM OFERTA"), Ui.Grey),
            "later" => (T("BUY LATER", "COMPRAR DEPOIS"), Ui.Grey),
            _ => (T("FIND IN RAID", "ENCONTRADO EM RAID"), Ui.Red),
        };
        ui.PillLabel(new Rect(r.xMax - 144, r.y + 10, 136, 20), label, color);
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
            var style = new GUIStyle(ui.Header) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            style.normal.textColor = Ui.Orange;
            GUI.Label(r, Currency(tpl), style);
            return;
        }

        _icons.Draw(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), tpl);
    }

    private void DrawFooter(Rect r)
    {
        var ui = _ui;
        var plan = _plan;
        int items = plan?.Purchases.Sum(p => p.Count) ?? 0;
        bool enough = plan is null || plan.Cost.All(c => plan.Money.TryGetValue(c.Key, out var have) && have >= c.Value);
        bool can = !_busy && plan is not null && enough && plan.Operations.Count > 0 && !InRaid;

        string text = InRaid
            ? T("BUYING WORKS IN THE HIDEOUT", "COMPRAS SÓ NO ESCONDERIJO")
            : items == 0
            ? T("NOTHING TO BUY", "NADA PARA COMPRAR")
            : !enough ? T("NOT ENOUGH MONEY", "DINHEIRO INSUFICIENTE")
            : T($"REVIEW {items} ITEMS", $"REVISAR {items} ITENS");
        if (ui.FlatButton(new Rect(r.x, r.y + 14, r.width, 46), text, true, can))
        {
            _confirming = true;
            _scroll = Vector2.zero;
            _status = "";
        }

        if (_status.Length > 0)
        {
            GUI.Label(new Rect(r.x, r.y + 62, r.width, 18), _status, ui.Muted);
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

    // Splits what will be bought into items to donate for upgrades and craft ingredients
    private (List<ConfirmLine> donate, List<ConfirmLine> craft) BuildConfirm(PlanResponse plan)
    {
        var bought = plan.Purchases.GroupBy(p => p.Tpl).ToDictionary(g => g.Key, g => (
            count: g.Sum(p => p.Count),
            rub: g.Sum(p => p.Rub),
            source: string.Join(" + ", g.Select(p => p.Source == "trader" ? T("trader", "comerciante") : T("flea market", "mercado")).Distinct())));

        var donate = new List<ConfirmLine>();
        var craft = new List<ConfirmLine>();
        foreach (var pair in bought)
        {
            string tpl = pair.Key;
            var (count, rub, source) = pair.Value;
            var forAreas = plan.Areas.SelectMany(a => a.Items.Where(i => i.Tpl == tpl && i.Missing > 0 && (i.Plan == "buy" || i.Plan == "partial")).Select(i => (a, i))).ToList();
            int toDonate = Math.Min(count, forAreas.Sum(x => x.i.Missing));
            int toCraft = count - toDonate;
            if (toDonate > 0)
            {
                donate.Add(new ConfirmLine
                {
                    Tpl = tpl, Count = toDonate, Rub = rub * toDonate / count, Source = source,
                    Detail = string.Join(", ", forAreas.Select(x => ((EAreaType)x.a.Type).LocalizeAreaName()).Distinct()),
                });
            }

            if (toCraft > 0)
            {
                var forCrafts = plan.Crafts.Where(c => c.Ingredients.Any(i => i.Tpl == tpl && i.Missing > 0));
                craft.Add(new ConfirmLine
                {
                    Tpl = tpl, Count = toCraft, Rub = rub * toCraft / count, Source = source,
                    Detail = string.Join(", ", forCrafts.Select(c => $"{Name(c.Tpl)} ({((EAreaType)c.Area).LocalizeAreaName()})").Distinct()),
                });
            }
        }

        return (donate, craft);
    }

    private void DrawConfirm(PlanResponse plan, float width)
    {
        var (donate, craft) = BuildConfirm(plan);
        GUILayout.Label(T("REVIEW PURCHASE", "REVISAR COMPRA"), _ui.Title, GUILayout.Height(32));
        GUILayout.Label(T("Hover an item to see what it is for and where it comes from.", "Passe o mouse num item para ver para que serve e de onde vem."), _ui.Muted, GUILayout.Height(18));
        GUILayout.Space(12);
        DrawTiles(T("TO DONATE FOR UPGRADES", "PARA DOAR NAS MELHORIAS"), T("Donate to", "Doar para"), donate, width, Ui.Orange);
        DrawTiles(T("CRAFT INGREDIENTS", "INGREDIENTES DE CRAFT"), T("To craft", "Para craftar"), craft, width, Ui.Cream);
    }

    private void DrawTiles(string title, string detailPrefix, List<ConfirmLine> lines, float width, Color accent)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var ui = _ui;
        var header = GUILayoutUtility.GetRect(width, 30);
        GUI.DrawTexture(new Rect(header.x, header.y + 6, 3, 18), ui.Tint(accent, 1));
        GUI.Label(new Rect(header.x + 12, header.y, width, header.height),
            $"{title}   <color=#9a9a94>{lines.Sum(l => l.Count)} {T("items", "itens")}  ·  ~{Short(lines.Sum(l => l.Rub))} ₽</color>", ui.Header);
        GUILayout.Space(6);

        const float tileW = 92, tileH = 112, gap = 8;
        int perRow = Math.Max(1, (int)((width + gap) / (tileW + gap)));
        int rows = (lines.Count + perRow - 1) / perRow;
        var grid = GUILayoutUtility.GetRect(width, rows * (tileH + gap));
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var tile = new Rect(grid.x + i % perRow * (tileW + gap), grid.y + i / perRow * (tileH + gap), tileW, tileH);
            bool hover = tile.Contains(Event.current.mousePosition);
            ui.Box(tile, ui.Card, hover ? ui.Tint(accent, 0.8f) : ui.Line);
            DrawIcon(new Rect(tile.x + 8, tile.y + 8, tileW - 16, tileW - 16), line.Tpl);

            var badgeText = "x" + line.Count;
            float badgeW = ui.Badge.CalcSize(new GUIContent(badgeText)).x + 10;
            var badge = new Rect(tile.xMax - badgeW - 4, tile.y + 4, badgeW, 16);
            GUI.DrawTexture(badge, ui.Tint(accent, 1));
            GUI.Label(badge, badgeText, ui.Badge);

            GUI.Label(new Rect(tile.x + 4, tile.y + tileW - 4, tileW - 8, 16), Name(line.Tpl), ui.TileName);
            var price = new GUIStyle(ui.TileName);
            price.normal.textColor = Ui.Grey;
            GUI.Label(new Rect(tile.x + 4, tile.y + tileW + 10, tileW - 8, 14), Short(line.Rub) + " ₽", price);

            if (hover)
            {
                _tooltip = $"<b>{Name(line.Tpl)} x{line.Count}</b>\n{detailPrefix}: {line.Detail}\n{T("From", "De")}: {line.Source}\n~{line.Rub:N0} ₽";
            }
        }

        GUILayout.Space(14);
    }

    private void DrawConfirmFooter(Rect r)
    {
        var ui = _ui;
        if (_plan is { } plan)
        {
            var cost = string.Join("  +  ", plan.Cost.Select(c => $"{c.Value:N0} {Currency(c.Key)}"));
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 22), $"<color=#9a9a94>{T("Total", "Total")}</color>   <b>{cost}</b>", ui.Header);
        }

        if (ui.FlatButton(new Rect(r.x, r.y + 38, 170, 46), T("BACK", "VOLTAR"), false, !_busy))
        {
            _confirming = false;
            _scroll = Vector2.zero;
        }

        string confirm = _busy ? _status : T("CONFIRM PURCHASE", "CONFIRMAR COMPRA");
        if (ui.FlatButton(new Rect(r.x + 180, r.y + 38, r.width - 180, 46), confirm, true, !_busy && _plan?.Operations.Count > 0))
        {
            Buy();
        }

        if (!_busy && _status.Length > 0)
        {
            GUI.Label(new Rect(r.x, r.y + 86, r.width, 16), _status, ui.Muted);
        }
    }

    private static string Short(long value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000d).ToString("0.##") + "M",
        >= 10_000 => (value / 1000d).ToString("0.#") + "k",
        _ => value.ToString("N0"),
    };
}
