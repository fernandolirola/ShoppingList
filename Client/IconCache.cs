using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace ShoppingList.Client;

/// <summary>The game's own item icons, drawn with IMGUI.</summary>
internal sealed class IconCache
{
    // The game's icon loader queues renders and fills the icon later; rendering many at once directly leaves some blank
    private readonly Dictionary<string, ItemIcon?> _icons = new();

    public void Draw(Rect rect, string tpl)
    {
        if (!_icons.TryGetValue(tpl, out var icon))
        {
            _icons[tpl] = icon = Load(tpl);
        }

        var sprite = icon?.Sprite;
        if (sprite == null || sprite.texture == null)
        {
            return;
        }

        // Fit the icon's grid shape (1x1, 2x1...) inside the square without stretching
        var tr = sprite.textureRect;
        float scale = Mathf.Min(rect.width / tr.width, rect.height / tr.height);
        var fitted = new Rect(rect.x + (rect.width - tr.width * scale) / 2, rect.y + (rect.height - tr.height * scale) / 2, tr.width * scale, tr.height * scale);
        var tex = sprite.texture;
        GUI.DrawTextureWithTexCoords(fitted, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
    }

    private static ItemIcon? Load(string tpl)
    {
        try
        {
            var item = Singleton<ItemFactory>.Instance.CreateItem("shoppinglist" + tpl, tpl, null);
            return ItemViewFactory.LoadItemIcon(item);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"No icon for {tpl}: {ex.Message}");
            return null;
        }
    }
}
