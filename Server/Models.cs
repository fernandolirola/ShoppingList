using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace ShoppingList;

public record PlanRequest : IRequestData
{
    [JsonPropertyName("excludeAll")] public bool ExcludeAll { get; init; }
    // Empty means every area whose other requirements are already met
    [JsonPropertyName("areas")] public List<int>? Areas { get; init; }
    [JsonPropertyName("crafts")] public bool Crafts { get; init; } = true;
    // Area type to the highest level to buy for; areas left out stop at their next level
    [JsonPropertyName("targets")] public Dictionary<int, int>? Targets { get; init; }
    // The overview also lists later levels of areas that are not ready yet
    [JsonPropertyName("overview")] public bool Overview { get; init; }
    // Nothing can be bought in raid, so the offer search is skipped
    [JsonPropertyName("raid")] public bool Raid { get; init; }
}

public record PlanResponse
{
    [JsonPropertyName("availableAreas")] public List<AreaPlan> AvailableAreas { get; init; } = [];
    [JsonPropertyName("areas")] public List<AreaPlan> Areas { get; init; } = [];
    [JsonPropertyName("crafts")] public List<CraftPlan> Crafts { get; init; } = [];
    [JsonPropertyName("future")] public List<AreaPlan> Future { get; init; } = [];
    [JsonPropertyName("purchases")] public List<Purchase> Purchases { get; init; } = [];
    [JsonPropertyName("unavailable")] public List<Need> Unavailable { get; init; } = [];
    [JsonPropertyName("cost")] public Dictionary<string, long> Cost { get; init; } = [];
    [JsonPropertyName("money")] public Dictionary<string, long> Money { get; init; } = [];
    [JsonPropertyName("totalRub")] public long TotalRub { get; set; }
    [JsonPropertyName("fleaUnlocked")] public bool FleaUnlocked { get; set; }
    [JsonPropertyName("operations")] public List<Dictionary<string, object>> Operations { get; init; } = [];
}

public record AreaPlan
{
    [JsonPropertyName("type")] public int Type { get; init; }
    [JsonPropertyName("level")] public int Level { get; init; }
    [JsonPropertyName("ready")] public bool Ready { get; init; }
    [JsonPropertyName("maxLevel")] public int MaxLevel { get; init; }
    // What still blocks this level, besides its items
    [JsonPropertyName("locks")] public List<Lock> Locks { get; init; } = [];
    [JsonPropertyName("items")] public List<Need> Items { get; init; } = [];
}

public record Lock
{
    // "area" or "trader"
    [JsonPropertyName("kind")] public string Kind { get; init; } = "";
    [JsonPropertyName("area")] public int Area { get; init; }
    [JsonPropertyName("trader")] public string Trader { get; init; } = "";
    [JsonPropertyName("level")] public int Level { get; init; }
}

public record CraftPlan
{
    [JsonPropertyName("tpl")] public string Tpl { get; init; } = "";
    [JsonPropertyName("missing")] public int Missing { get; init; }
    [JsonPropertyName("recipe")] public string Recipe { get; init; } = "";
    [JsonPropertyName("area")] public int Area { get; init; }
    [JsonPropertyName("runs")] public int Runs { get; init; }
    [JsonPropertyName("ingredients")] public List<Need> Ingredients { get; init; } = [];
}

public record Need
{
    [JsonPropertyName("tpl")] public string Tpl { get; init; } = "";
    [JsonPropertyName("required")] public int Required { get; init; }
    [JsonPropertyName("have")] public int Have { get; set; }
    [JsonPropertyName("donated")] public int Donated { get; init; }
    // -1 unknown, 0 common, 1 rare, 2 super rare
    [JsonPropertyName("rarity")] public int Rarity { get; set; } = -1;
    [JsonPropertyName("missing")] public int Missing { get; set; }
    [JsonPropertyName("fir")] public bool Fir { get; init; }
    [JsonPropertyName("tool")] public bool Tool { get; init; }
    // "ok", "buy", "partial", "craft", "craftable" (FIR with a usable recipe, not planned), "money", "none" (FIR with no unlocked craft),
    // "unavailable" or "later" (future level)
    [JsonPropertyName("plan")] public string Plan { get; set; } = "none";
}

public record Purchase
{
    [JsonPropertyName("tpl")] public string Tpl { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("source")] public string Source { get; init; } = "";
    [JsonPropertyName("trader")] public string Trader { get; init; } = "";
    [JsonPropertyName("currency")] public string Currency { get; init; } = "";
    [JsonPropertyName("price")] public long Price { get; init; }
    [JsonPropertyName("rub")] public long Rub { get; init; }
}
