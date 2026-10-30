using System;
using System.IO;
using EFT.Hideout;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ShoppingList.Client;

/// <summary>A copy of the hideout's own left toolbar button with a cart icon.</summary>
internal sealed class ToolbarButton
{
    private static readonly AccessTools.FieldRef<HideoutScreenOverlay, ComplementaryButton> Customization = AccessTools.FieldRefAccess<HideoutScreenOverlay, ComplementaryButton>("_customizationButton");
    private static readonly AccessTools.FieldRef<HideoutScreenOverlay, ComplementaryButton> Generator = AccessTools.FieldRefAccess<HideoutScreenOverlay, ComplementaryButton>("_generatorButton");
    private static readonly AccessTools.FieldRef<ComplementaryButton, Sprite> DefaultIcon = AccessTools.FieldRefAccess<ComplementaryButton, Sprite>("_defaultIcon");
    private static readonly AccessTools.FieldRef<ComplementaryButton, Sprite> SelectedIcon = AccessTools.FieldRefAccess<ComplementaryButton, Sprite>("_selectedIcon");

    private ComplementaryButton? _button;
    private ComplementaryButton? _template;
    private bool _failed;

    public bool Exists => _button != null;

    // Called every frame while the hideout overlay is around; builds the button once, then mirrors the template's visibility
    public void Sync(HideoutScreenOverlay overlay, Func<string> tooltip, Action onClick)
    {
        if (_failed)
        {
            return;
        }

        try
        {
            if (_button == null)
            {
                Create(overlay, tooltip, onClick);
            }

            bool visible = _template != null && _template.gameObject.activeSelf;
            if (_button != null && _button.gameObject.activeSelf != visible)
            {
                if (visible) Show(tooltip, onClick); else _button.gameObject.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            _failed = true;
            Plugin.Log.LogError($"Toolbar button not added, using the fallback button: {ex}");
        }
    }

    private void Create(HideoutScreenOverlay overlay, Func<string> tooltip, Action onClick)
    {
        _template = Customization(overlay);
        var generator = Generator(overlay);
        var copy = UnityEngine.Object.Instantiate(_template.gameObject, _template.transform.parent, false);
        copy.name = "ShoppingListButton";
        _button = copy.GetComponent<ComplementaryButton>();

        // Below the last button, one step further down
        var last = (RectTransform)generator.transform;
        var rect = (RectTransform)copy.transform;
        if (copy.transform.parent.GetComponent<LayoutGroup>() == null)
        {
            var step = last.anchoredPosition - ((RectTransform)_template.transform).anchoredPosition;
            rect.anchoredPosition = last.anchoredPosition + step;
        }

        copy.transform.SetSiblingIndex(last.GetSiblingIndex() + 1);

        var sprite = LoadIcon(DefaultIcon(_template));
        DefaultIcon(_button) = sprite;
        SelectedIcon(_button) = sprite;
        _button.SetIcon(sprite);
        Show(tooltip, onClick);
    }

    private void Show(Func<string> tooltip, Action onClick)
    {
        _button!.Show(_ => onClick());
        _button.SetTooltipMessages(tooltip, tooltip);
        _button.SetSelectedStatus(false);
    }

    // The button calls SetNativeSize on its icon, so the cart gets the same native size as the icon it replaces
    private static Texture2D? _cart;

    public static Texture2D CartTexture
    {
        get
        {
            if (_cart == null)
            {
                using var stream = typeof(ToolbarButton).Assembly.GetManifestResourceStream("cart.png")!;
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                _cart = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _cart.LoadImage(memory.ToArray());
            }

            return _cart;
        }
    }

    public static Sprite CartSprite(float pixelsPerUnit = 100f) =>
        Sprite.Create(CartTexture, new Rect(0, 0, CartTexture.width, CartTexture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);

    private static Sprite LoadIcon(Sprite reference)
    {
        var texture = CartTexture;
        float nativeWidth = reference.rect.width / reference.pixelsPerUnit;
        return CartSprite(texture.width / (nativeWidth * 0.85f));
    }
}
