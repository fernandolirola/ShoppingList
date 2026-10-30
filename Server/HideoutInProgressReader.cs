using System.Collections;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace ShoppingList;

/// <summary>
/// Items already donated with HideoutInProgress count toward an upgrade, so they are subtracted from what is missing.
/// Read by reflection so the mod works the same when HideoutInProgress is not installed.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class HideoutInProgressReader
{
    public Dictionary<int, Dictionary<string, int>> GetDonations(MongoId profileId)
    {
        var result = new Dictionary<int, Dictionary<string, int>>();
        try
        {
            var patch = AccessTools.TypeByName("HideoutInProgress.Server.StartUpgradePatch");
            var helperType = AccessTools.TypeByName("HideoutInProgress.Server.ProfileDataHelper");
            var dataType = AccessTools.TypeByName("HideoutInProgress.Server.ProfileData");
            var helper = patch is null ? null : AccessTools.Field(patch, "ProfileDataHelper")?.GetValue(null);
            if (helper is null || helperType is null || dataType is null)
            {
                return result;
            }

            var data = AccessTools.Method(helperType, "GetProfileData")?.Invoke(helper, [profileId]);
            if (AccessTools.Property(dataType, "AreaProgresses")?.GetValue(data) is not IDictionary progresses)
            {
                return result;
            }

            foreach (DictionaryEntry entry in progresses)
            {
                if (entry.Value is Dictionary<string, int> donated)
                {
                    result[Convert.ToInt32(entry.Key)] = new Dictionary<string, int>(donated, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            // HideoutInProgress changed shape; planning without donations is still correct, just less helpful
        }

        return result;
    }
}
