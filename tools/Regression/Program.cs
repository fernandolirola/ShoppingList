using System.Collections;
using System.Reflection;
using System.Text.Json;
using ShoppingList;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
resolver.Modifiers.Add(t => { foreach (var p in t.Properties) p.IsRequired = false; });
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false, TypeInfoResolver = resolver };
options.Converters.Add(new IdConverter());
const string tpl = "5d1b376e86f774252519444e";
var flags = BindingFlags.NonPublic | BindingFlags.Instance;
var table = JsonSerializer.Deserialize<HideoutTable>("""
{"areas":[{"type":19,"enabled":true,"stages":{"1":{"requirements":[{"type":"Area","areaType":10,"requiredLevel":2}]},"2":{"requirements":[]}}}],"production":{"recipes":[]}}
""", options)!;
Planner Create() {
 var ctor = typeof(Planner).GetConstructors().Single();
 return (Planner)ctor.Invoke(ctor.GetParameters().Select(p => p.ParameterType == typeof(HideoutTable) ? (object)table : null).ToArray());
}
PmcData Profile(int level, bool constructing = false) => JsonSerializer.Deserialize<PmcData>($$$"""
{"Hideout":{"Areas":[{"type":19,"level":{{{level}}},"constructing":{{{constructing.ToString().ToLowerInvariant()}}}}]}}
""", options)!;
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
void Recipe(string requirements = "[]", bool locked = false) {
 var data = JsonSerializer.Deserialize<HideoutTable>($$$"""
{"production":{"recipes":[{"_id":"5d5c1fd4d582a500650132f0","areaType":19,"requirements":{{{requirements}}},"endProduct":"{{{tpl}}}","count":1,"locked":{{{locked.ToString().ToLowerInvariant()}}}}]}}
""", options)!;
 typeof(HideoutTable).GetProperty("Production")!.SetValue(table, data.Production);
}
object Craft(int level, bool constructing = false) => typeof(Planner).GetMethod("PlanCraft", flags)!.Invoke(Create(), [Profile(level, constructing), new MongoId(tpl), 1, new Dictionary<MongoId,int>(), new Dictionary<MongoId,int>(), null]);
Recipe();
Check(Craft(0) is null, "recipe without Area requirement rejected for unbuilt station");
Check(Craft(0, true) is null, "initial construction does not unlock crafts");
Check(Craft(1) is CraftPlan, "built station allows recipe without Area requirement");
Recipe("[{\"type\":\"Area\",\"areaType\":19,\"requiredLevel\":2}]");
Check(Craft(1) is null, "station below recipe level rejected");
Check(Craft(1, true) is null, "unfinished upgrade does not unlock next-level craft");
Check(Craft(2) is CraftPlan, "recipe level met");
Recipe(locked: true);
Check(Craft(2) is null, "locked recipe stays hidden");
var select = typeof(Planner).GetMethod("SelectAreas", flags)!;
int Count(bool all, List<int> wanted = null) => ((IEnumerable)select.Invoke(Create(), [Profile(0), wanted, all])!).Cast<object>().Count();
Check(Count(false) == 0, "locked upgrade not automatically purchased");
Check(Count(true) == 1, "overview includes locked next upgrade");
Check(Count(false, [19]) == 1, "locked upgrade can be explicitly selected");
Check(Count(false, [-1]) == 0, "empty selection plans no purchases");
PlanResponse Future(int type, int next, Dictionary<int, int> targets) {
 var response = new PlanResponse();
 response.Areas.Add(new AreaPlan { Type = type, Level = next });
 typeof(Planner).GetMethod("PlanFuture", flags)!.Invoke(Create(), [Profile(next - 1), new PlanRequest { Targets = targets }, response, new Dictionary<MongoId,int>(), new Dictionary<MongoId,int>(), new Dictionary<MongoId,(int, int)>()]);
 return response;
}
var future = Future(19, 1, new() { [19] = 2 });
Check(future.Future.Count == 1 && future.Future[0].Level == 2, "target level plans the levels after the next one");
Check(Future(19, 1, new()).Future.Count == 0, "no target stops at the next level");
Check(Future(19, 1, new() { [19] = 9 }).Future.Count == 1, "target above the last level stops at the last level");
var maxLevel = typeof(Planner).GetMethod("MaxLevel", BindingFlags.NonPublic | BindingFlags.Static)!;
Check((int)maxLevel.Invoke(null, [table.Areas.Single()])! == 2, "max level is the last manual stage");
Check(((IEnumerable)select.Invoke(Create(), [Profile(0, true), null, true])!).Cast<object>().Count() == 0, "construction excluded from next-upgrade catalog");
Check(((IEnumerable)select.Invoke(Create(), [Profile(2), null, true])!).Cast<object>().Count() == 0, "maxed area excluded from next-upgrade catalog");
table = JsonSerializer.Deserialize<HideoutTable>("""
{"areas":[{"type":25,"enabled":true,"stages":{"1":{"autoUpgrade":true,"requirements":[]},"2":{"autoUpgrade":true,"requirements":[]}}}],"production":{"recipes":[]}}
""", options)!;
Check(Count(true) == 0, "automatic storage stage hidden from overview");
Check(Count(false, [25]) == 0, "explicit selection cannot add automatic storage stage");
Check(Future(25, 1, new() { [25] = 2 }).Future.Count == 0, "automatic storage stages hidden from future upgrades");
Check((int)maxLevel.Invoke(null, [table.Areas.Single()])! == 0, "automatic stages do not count toward max level");
var foreign = typeof(Planner).GetMethod("IsForeignCurrency", BindingFlags.NonPublic | BindingFlags.Static)!;
Check((bool)foreign.Invoke(null, [new MongoId("5696686a4bdc2da3298b456a")])! && (bool)foreign.Invoke(null, [new MongoId("569668774bdc2da2298b4568")])!, "dollars and euros can be bought");
Check(!(bool)foreign.Invoke(null, [new MongoId("5449016a4bdc2d6f028b456f")])!, "roubles are never bought");
var order = new PlanResponse();
foreach (var t in new[] { tpl, "5696686a4bdc2da3298b456a" })
{
 order.Purchases.Add(new Purchase { Tpl = t, Count = 1 });
 order.Operations.Add(new Dictionary<string, object> { ["tpl"] = t });
}
typeof(Planner).GetMethod("BuyCurrencyForPurchases", flags)!.Invoke(Create(), [order, null, new Dictionary<MongoId,int>(), new Dictionary<MongoId,int>()]);
Check(order.Purchases[0].Tpl == "5696686a4bdc2da3298b456a" && (string)order.Operations[0]["tpl"] == order.Purchases[0].Tpl, "currency is bought first, with its operation");
Console.WriteLine($"{checks} regression checks passed");





sealed class IdConverter : System.Text.Json.Serialization.JsonConverter<MongoId> {
 public override MongoId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(reader.GetString()!);
 public override void Write(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}



