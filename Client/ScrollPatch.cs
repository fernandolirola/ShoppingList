using EFT.Hideout;
using EFT.InputSystem;
using HarmonyLib;
using static EFT.InputSystem.InputNode;

namespace ShoppingList.Client;

/// <summary>The hideout zooms on the same wheel input the list scrolls with, so zoom is held off while the list is open.</summary>
[HarmonyPatch(typeof(HideoutScreenOverlay), nameof(HideoutScreenOverlay.TranslateCommand))]
internal static class ScrollPatch
{
    internal static bool Blocked;

    [HarmonyPrefix]
    private static bool Prefix(ECommand command, ref ETranslateResult __result)
    {
        if (Blocked && command is ECommand.ScrollPrevious or ECommand.ScrollNext)
        {
            __result = ETranslateResult.Block;
            return false;
        }

        return true;
    }
}
