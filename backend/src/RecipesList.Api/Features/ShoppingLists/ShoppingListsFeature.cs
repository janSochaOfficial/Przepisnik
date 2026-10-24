using Microsoft.AspNetCore.Http.HttpResults;
using RecipesList.Api.Features.Fridges;
using RecipesList.Core.ShoppingLists;

namespace RecipesList.Api.Features.ShoppingLists;

/// <summary>The per-fridge shopping list (API.md §7).</summary>
internal static class ShoppingListsFeature
{
    public static IServiceCollection AddShoppingLists(this IServiceCollection services)
    {
        services.AddScoped<IShoppingListService, ShoppingListService>();
        return services;
    }

    public static IEndpointRouteBuilder MapShoppingLists(this IEndpointRouteBuilder api)
    {
        api.MapGet("/fridges/{id:guid}/shopping-list", async Task<Ok<ShoppingListItemDto[]>> (Guid id, IShoppingListService list, CancellationToken ct) =>
                TypedResults.Ok((await list.ListAsync(id, ct)).Select(ShoppingListItemDto.From).ToArray()))
            .WithName("GetShoppingList")
            .WithSummary("The fridge's shopping list")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/fridges/{id:guid}/shopping-list/items", async Task<Ok<ShoppingListItemDto[]>> (
                Guid id, ShoppingListItemsWriteRequest request, IShoppingListService list, CancellationToken ct) =>
            {
                var inputs = request.Items.Select(i => new ShoppingListItemInput(i.ProductId, i.Text, i.Amount)).ToList();
                return TypedResults.Ok((await list.AddAsync(id, inputs, ct)).Select(ShoppingListItemDto.From).ToArray());
            })
            .WithName("AddShoppingListItems")
            .WithSummary("Add or merge shopping-list items")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPatch("/fridges/{id:guid}/shopping-list/items/{itemId:guid}", async Task<Ok<ShoppingListItemDto>> (
                Guid id, Guid itemId, ShoppingListItemPatchRequest request, IShoppingListService list, CancellationToken ct) =>
            {
                var patch = new ShoppingListItemPatchInput(request.Version, request.Checked, request.Amount, request.Text);
                return TypedResults.Ok(ShoppingListItemDto.From(await list.PatchAsync(id, itemId, patch, ct)));
            })
            .WithName("PatchShoppingListItem")
            .WithSummary("Check off or edit an item")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/fridges/{id:guid}/shopping-list/items/{itemId:guid}", async Task<NoContent> (
                Guid id, Guid itemId, IShoppingListService list, CancellationToken ct) =>
            {
                await list.DeleteAsync(id, itemId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteShoppingListItem")
            .WithSummary("Remove an item")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/fridges/{id:guid}/shopping-list/complete", async Task<Ok<CompleteShoppingListResultDto>> (
                Guid id, CompleteShoppingListRequest request, IShoppingListService list, CancellationToken ct) =>
            {
                var result = await list.CompleteAsync(id, request.MoveToFridge, ct);
                return TypedResults.Ok(new CompleteShoppingListResultDto(result.Removed, [.. result.CreatedFridgeItems.Select(FridgeItemDto.From)]));
            })
            .WithName("CompleteShoppingList")
            .WithSummary("Remove bought items, optionally moving them to the fridge")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }
}
