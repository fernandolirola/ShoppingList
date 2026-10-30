using System;
using EFT.Hideout;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShoppingList.Client;

/// <summary>A copy of the area window's Close button, placed left of it, that opens the list for that area.</summary>
internal sealed class AreaButton
{
    private static readonly AccessTools.FieldRef<AreaScreenSubstrate, Button> CloseButton = AccessTools.FieldRefAccess<AreaScreenSubstrate, Button>("_closeButton");

    private RectTransform? _close;
    private RectTransform? _rect;
    private TextMeshProUGUI? _label;
    private bool _failed;

    public bool Exists => _rect != null;

    public void Sync(AreaScreenSubstrate screen, bool visible, string text, Sprite icon, Action onClick)
    {
        if (_failed)
        {
            return;
        }

        try
        {
            if (_rect == null)
            {
                Create(screen, icon, onClick);
            }

            _rect!.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (_label!.text != text)
            {
                _label.text = text;
            }

            // The Close button sizes itself to its text, so follow its left edge every frame
            _rect.anchoredPosition = new Vector2(_close!.anchoredPosition.x - _close.rect.width - 10, _close.anchoredPosition.y);
        }
        catch (Exception ex)
        {
            _failed = true;
            Plugin.Log.LogError($"Area button not added, using the fallback button: {ex}");
        }
    }

    private void Create(AreaScreenSubstrate screen, Sprite icon, Action onClick)
    {
        var close = CloseButton(screen);
        _close = (RectTransform)close.transform;
        var copy = UnityEngine.Object.Instantiate(close.gameObject, close.transform.parent, false);
        copy.name = "ShoppingListAreaButton";
        _rect = (RectTransform)copy.transform;
        foreach (var localized in copy.GetComponentsInChildren<LocalizedText>(true))
        {
            UnityEngine.Object.DestroyImmediate(localized);
        }

        _label = copy.GetComponentInChildren<TextMeshProUGUI>(true);
        // The red close plate doesn't take a new sprite or color, so it is swapped for a cart icon of our own
        var plate = copy.transform.Find("RedButton");
        int index = plate != null ? plate.GetSiblingIndex() : copy.transform.childCount;
        if (plate != null)
        {
            UnityEngine.Object.DestroyImmediate(plate.gameObject);
        }

        var cart = new GameObject("Cart", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cart.transform.SetParent(copy.transform, false);
        cart.transform.SetSiblingIndex(index);
        var cartImage = cart.GetComponent<Image>();
        cartImage.sprite = icon;
        cartImage.color = Ui.Orange;
        cartImage.preserveAspect = true;
        var layout = cart.GetComponent<LayoutElement>();
        layout.preferredWidth = layout.minWidth = 18;
        layout.preferredHeight = layout.minHeight = 15;

        // The copy keeps the game's click wiring, so it is replaced with a handler of our own
        var button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        UnityEngine.Object.DestroyImmediate(button);
        copy.AddComponent<ClickRelay>().OnClick = onClick;
        // Raycasts need a Graphic on the object that holds the click handler
        if (copy.GetComponent<Graphic>() == null)
        {
            copy.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        }

        foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = graphic.gameObject == copy;
        }

        Plugin.Log.LogInfo("Area button added");
    }

    private sealed class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action? OnClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnClick?.Invoke();
            }
        }
    }
}
