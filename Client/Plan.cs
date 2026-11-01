using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShoppingList.Client;

public class PlanResponse
{
    [JsonProperty("availableAreas")] public List<AreaPlan> AvailableAreas = new();
    [JsonProperty("error")] public string? Error;
    [JsonProperty("areas")] public List<AreaPlan> Areas = new();
    [JsonProperty("crafts")] public List<CraftPlan> Crafts = new();
    [JsonProperty("future")] public List<AreaPlan> Future = new();
    [JsonProperty("purchases")] public List<Purchase> Purchases = new();
    [JsonProperty("unavailable")] public List<Need> Unavailable = new();
    [JsonProperty("cost")] public Dictionary<string, long> Cost = new();
    [JsonProperty("money")] public Dictionary<string, long> Money = new();
    [JsonProperty("totalRub")] public long TotalRub;
    [JsonProperty("fleaUnlocked")] public bool FleaUnlocked;
    [JsonProperty("operations")] public List<JObject> Operations = new();
}

public class AreaPlan
{
    [JsonProperty("type")] public int Type;
    [JsonProperty("level")] public int Level;
    [JsonProperty("ready")] public bool Ready;
    [JsonProperty("items")] public List<Need> Items = new();
}

public class CraftPlan
{
    [JsonProperty("tpl")] public string Tpl = "";
    [JsonProperty("missing")] public int Missing;
    [JsonProperty("area")] public int Area;
    [JsonProperty("runs")] public int Runs;
    [JsonProperty("ingredients")] public List<Need> Ingredients = new();
}

public class Need
{
    [JsonProperty("tpl")] public string Tpl = "";
    [JsonProperty("required")] public int Required;
    [JsonProperty("have")] public int Have;
    [JsonProperty("donated")] public int Donated;
    [JsonProperty("carried")] public int Carried;
    [JsonProperty("rarity")] public int Rarity = -1;
    [JsonProperty("missing")] public int Missing;
    [JsonProperty("fir")] public bool Fir;
    [JsonProperty("tool")] public bool Tool;
    [JsonProperty("plan")] public string Plan = "";
}

public class Purchase
{
    [JsonProperty("tpl")] public string Tpl = "";
    [JsonProperty("count")] public int Count;
    [JsonProperty("source")] public string Source = "";
    [JsonProperty("currency")] public string Currency = "";
    [JsonProperty("price")] public long Price;
    [JsonProperty("rub")] public long Rub;
}
