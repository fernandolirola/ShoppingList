using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Utils;

namespace ShoppingList;

[Injectable]
public sealed class ShoppingListRouter(JsonUtil jsonUtil, Planner planner, ISptLogger<ShoppingListRouter> logger) : StaticRouter(jsonUtil,
[
    new RouteAction<PlanRequest>("/shoppinglist/plan", (_, request, sessionId, _, _) =>
    {
        try
        {
            var plan = planner.Plan(sessionId, request);
            return new ValueTask<string>(jsonUtil.Serialize(plan, typeof(PlanResponse))!);
        }
        catch (Exception ex)
        {
            logger.Error($"[ShoppingList] Plan failed: {ex}");
            return new ValueTask<string>("{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.Message) + "}");
        }
    }),
]);

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class ShoppingListLoaded(ISptLogger<ShoppingListLoaded> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Success($"[ShoppingList] {typeof(ShoppingListLoaded).Assembly.GetName().Version!.ToString(3)} loaded");
        return Task.CompletedTask;
    }
}
