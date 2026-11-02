using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT.Hideout;
using UnityEngine;

namespace ShoppingList.Client;

/// <summary>The hideout's own area icons, kept once seen so the list shows them in raid too.</summary>
internal static class AreaIcons
{
    private static readonly Dictionary<int, Sprite> Sprites = new();
    private static bool _scanned;

    public static Sprite? Get(int type)
    {
        if (Sprites.TryGetValue(type, out var sprite))
        {
            return sprite;
        }

        if (!_scanned && Singleton<HideoutRepresentation>.Instantiated && Singleton<HideoutRepresentation>.Instance is { } hideout)
        {
            foreach (var area in hideout.AreaDatas.Where(a => a.Template?.AreaIcon != null))
            {
                Sprites[(int)area.Template.Type] = area.Template.AreaIcon;
            }

            // The areas may not be loaded yet on the first look
            _scanned = Sprites.Count > 0;
        }

        return Sprites.TryGetValue(type, out sprite) ? sprite : null;
    }
}
