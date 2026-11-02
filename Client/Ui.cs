using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShoppingList.Client;

internal sealed class Ui
{
    public static readonly Color Orange = new(242 / 255f, 170 / 255f, 60 / 255f);
    public static readonly Color Cream = new(239 / 255f, 235 / 255f, 224 / 255f);
    public static readonly Color Green = new(0.47f, 0.8f, 0.42f);
    public static readonly Color Red = new(0.88f, 0.36f, 0.3f);
    public static readonly Color Amber = new(0.93f, 0.78f, 0.35f);
    public static readonly Color Grey = new(0.6f, 0.6f, 0.58f);

    public static float Scale => Screen.height / 1080f;

    public readonly Texture2D Bg = Solid(new Color(0.055f, 0.055f, 0.055f, 0.97f));
    public readonly Texture2D Card = Solid(new Color(0.095f, 0.095f, 0.095f, 1f));
    public readonly Texture2D RowAlt = Solid(new Color(1, 1, 1, 0.025f));
    public readonly Texture2D Line = Solid(new Color(1, 1, 1, 0.08f));
    public readonly Texture2D Cell = Solid(new Color(0.15f, 0.15f, 0.15f, 1f));
    public readonly Texture2D CellBorder = Solid(new Color(1, 1, 1, 0.12f));
    public readonly Texture2D OrangeTex = Solid(Orange);
    public readonly Texture2D OrangeHover = Solid(Color.Lerp(Orange, Color.white, 0.18f));
    public readonly Texture2D OrangeDim = Solid(new Color(Orange.r, Orange.g, Orange.b, 0.16f));
    public readonly Texture2D Disabled = Solid(new Color(1, 1, 1, 0.08f));
    public readonly Texture2D Hover = Solid(new Color(1, 1, 1, 0.06f));
    public readonly Texture2D Clear = Solid(new Color(0, 0, 0, 0));
    public readonly Texture2D BlackTex = Solid(new Color(0, 0, 0, 0.8f));
    // White shapes tinted with GUI.color: IMGUI has no circles of its own. Made at the size they are drawn,
    // since a big texture squeezed down without mipmaps looks soft
    public static int DotSize => Mathf.RoundToInt(20 * Scale);
    public readonly Texture2D Circle = Shape(DotSize, (x, y) => Dist(x, y, 32, 32) <= 32);
    public readonly Texture2D Ring = Shape(DotSize, (x, y) => Dist(x, y, 32, 32) is <= 32 and >= 27);
    public readonly Texture2D Lock = Shape(Mathf.RoundToInt(10 * Scale), (x, y) =>
        // Padlock: a body with a round shackle on top
        x is >= 12 and <= 52 && y is >= 2 and <= 36 || y >= 36 && Dist(x, y, 32, 36) is >= 12 and <= 20);

    public readonly GUIStyle Brand, Title, Subtitle, Header, Text, Muted, ChipLabel, ChipValue, Pill, Badge, TileName,
        Center, ButtonText, Tooltip, Plain, Chevron;

    private readonly System.Collections.Generic.Dictionary<Color, Texture2D> _tints = new();

    public Ui()
    {
        var font = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f => f.name.ToLowerInvariant().Contains("bender"));

        Brand = Label(Orange, 11, FontStyle.Bold, font);
        Title = Label(Cream, 22, FontStyle.Bold, font);
        Subtitle = Label(Grey, 15, FontStyle.Normal, font);
        Header = Label(Cream, 15, FontStyle.Bold, font);
        Text = Label(Cream, 14, FontStyle.Normal, font);
        Muted = Label(Grey, 12, FontStyle.Normal, font);
        ChipLabel = Label(Grey, 11, FontStyle.Bold, font);
        ChipValue = Label(Cream, 18, FontStyle.Bold, font);
        Pill = Label(Cream, 11, FontStyle.Bold, font);
        Pill.alignment = TextAnchor.MiddleCenter;
        Badge = Label(new Color(0.07f, 0.07f, 0.07f), 11, FontStyle.Bold, font);
        Badge.alignment = TextAnchor.MiddleCenter;
        TileName = Label(Cream, 11, FontStyle.Normal, font);
        TileName.alignment = TextAnchor.UpperCenter;
        TileName.clipping = TextClipping.Clip;
        Center = Label(Grey, 14, FontStyle.Normal, font);
        Center.alignment = TextAnchor.MiddleCenter;
        Center.wordWrap = true;
        ButtonText = Label(new Color(0.07f, 0.07f, 0.07f), 16, FontStyle.Bold, font);
        ButtonText.alignment = TextAnchor.MiddleCenter;
        Tooltip = Label(Cream, 12, FontStyle.Normal, font);
        Tooltip.wordWrap = true;
        Tooltip.padding = new RectOffset(8, 8, 6, 6);
        Tooltip.normal.background = Solid(new Color(0.02f, 0.02f, 0.02f, 0.97f));
        // The game font has no arrow glyphs, so the default font draws them
        Chevron = Label(Grey, 11, FontStyle.Normal, null);
        Chevron.alignment = TextAnchor.MiddleCenter;
        Plain = new GUIStyle { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
    }

    // OnGUI runs several times a frame; a style made once per variant saves allocating new ones every call
    private readonly Dictionary<(GUIStyle, Color, int, TextAnchor, TextClipping), GUIStyle> _styled = new();

    public GUIStyle Styled(GUIStyle source, Color? color = null, int? size = null, TextAnchor? align = null, bool clip = false)
    {
        var key = (source, color ?? source.normal.textColor, size ?? source.fontSize, align ?? source.alignment, clip ? TextClipping.Clip : source.clipping);
        if (!_styled.TryGetValue(key, out var style))
        {
            _styled[key] = style = new GUIStyle(source) { fontSize = key.Item3, alignment = key.Item4, clipping = key.Item5 };
            style.normal.textColor = key.Item2;
        }

        return style;
    }

    public Texture2D Tint(Color c, float alpha)
    {
        var key = new Color(c.r, c.g, c.b, alpha);
        if (!_tints.TryGetValue(key, out var tex))
        {
            _tints[key] = tex = Solid(key);
        }

        return tex;
    }

    public void Box(Rect r, Texture2D fill, Texture2D? border = null)
    {
        GUI.DrawTexture(r, fill);
        if (border != null)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), border);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), border);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), border);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), border);
        }
    }

    public void PillLabel(Rect r, string text, Color color)
    {
        GUI.DrawTexture(r, Tint(color, 0.16f));
        GUI.DrawTexture(new Rect(r.x, r.y, 2, r.height), Tint(color, 0.9f));
        GUI.Label(r, text, Styled(Pill, color));
    }

    // Outline only, for statuses that inform rather than ask for an action
    public void QuietPill(Rect r, string text, Color color)
    {
        Box(r, Clear, Tint(color, 0.35f));
        GUI.Label(r, text, Styled(Pill, new Color(color.r, color.g, color.b, 0.75f)));
    }

    // A flat button drawn by hand: this game's IMGUI skin ignores style backgrounds
    public bool FlatButton(Rect r, string text, bool primary, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        var fill = !enabled ? Disabled : primary ? (hover ? OrangeHover : OrangeTex) : (hover ? Tint(Cream, 0.14f) : Tint(Cream, 0.07f));
        GUI.DrawTexture(r, fill);
        GUI.Label(r, text, Styled(ButtonText, !enabled ? Grey : primary ? new Color(0.07f, 0.07f, 0.07f) : Cream, r.height >= 36 ? 16 : 12));
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    public bool Checkbox(Rect r, bool value)
    {
        var box = new Rect(r.x, r.y + (r.height - 16) / 2, 16, 16);
        Box(box, value ? OrangeTex : Tint(Cream, 0.05f), value ? null : Tint(Cream, 0.35f));
        if (value)
        {
            // Same drawn check as the level track: the font's glyph sits off-center
            Tinted(new Rect(box.x + 2, box.y + 2, 12, 12), Check, new Color(0.07f, 0.07f, 0.07f));
        }

        return GUI.Button(r, GUIContent.none, GUIStyle.none) ? !value : value;
    }

    private static GUIStyle Label(Color color, int size, FontStyle style, Font? font)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, richText = true, wordWrap = false, alignment = TextAnchor.MiddleLeft };
        s.padding = new RectOffset(0, 0, 0, 0);
        s.margin = new RectOffset(0, 0, 0, 0);
        s.normal.textColor = color;
        if (font != null) s.font = font;
        return s;
    }

    // The game font's check glyph sits off-center, so the check is drawn like the other shapes
    public readonly Texture2D Check = Shape(Mathf.RoundToInt(12 * Scale), (x, y) =>
        Segment(x, y, 8, 34, 26, 16) <= 6 || Segment(x, y, 26, 16, 56, 48) <= 6);

    private static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
        return Dist(x, y, ax + t * dx, ay + t * dy);
    }

    private static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

    // Shape drawn in a 64 x 64 space, sampled 4 x 4 per pixel for smooth but sharp edges
    private static Texture2D Shape(int size, System.Func<float, float, bool> inside)
    {
        size = Mathf.Max(8, size);
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        float cell = 64f / size;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int hits = 0;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                if (inside((x + (sx + 0.5f) / 4) * cell, (y + (sy + 0.5f) / 4) * cell)) hits++;
            }

            t.SetPixel(x, y, new Color(1, 1, 1, hits / 16f));
        }

        t.Apply();
        return t;
    }

    public static void Tinted(Rect r, Texture2D shape, Color color)
    {
        var old = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
        GUI.DrawTexture(r, shape);
        GUI.color = old;
    }

    private static Texture2D Solid(Color color)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }
}
