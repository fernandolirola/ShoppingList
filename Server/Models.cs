using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace ShoppingList;

public record PlanRequest : IRequestData
{
    // Empty means every area whose other requirements are already met
    [JsonPropertyName("areas")] public List<int>? Areas { get; init; }
    [JsonPropertyName("crafts")] public bool Crafts { get; init; } = true;
}

public record PlanResponse
{
    [JsonPropertyName("areas")] public List<AreaPlan> Areas { get; init; } = [];
    [JsonPropertyName("crafts")] public List<CraftPlan> Crafts { get; init; } = [];
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
    [JsonPropertyName("items")] public List<Need> Items { get; init; } = [];
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
    [JsonPropertyName("missing")] public int Missing { get; set; }
    [JsonPropertyName("fir")] public bool Fir { get; init; }
    [JsonPropertyName("tool")] public bool Tool { get; init; }
    // "ok", "buy", "partial", "craft", "money", "none" (FIR with no unlocked craft) or "unavailable"
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
