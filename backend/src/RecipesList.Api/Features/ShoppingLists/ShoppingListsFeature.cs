namespace RecipesList.Api.Features.ShoppingLists;

/// <summary>The per-fridge shopping list (API.md §7). Implemented in B5.</summary>
internal static class ShoppingListsFeature
{
    public static IServiceCollection AddShoppingLists(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapShoppingLists(this IEndpointRouteBuilder api)
    {
        api.MapStub("GET", "/fridges/{id:guid}/shopping-list", "GetShoppingList", "The fridge's shopping list");
        api.MapStub("POST", "/fridges/{id:guid}/shopping-list/items", "AddShoppingListItems", "Add or merge shopping-list items");
        api.MapStub("PATCH", "/fridges/{id:guid}/shopping-list/items/{itemId:guid}", "PatchShoppingListItem", "Check off or edit an item");
        api.MapStub("DELETE", "/fridges/{id:guid}/shopping-list/items/{itemId:guid}", "DeleteShoppingListItem", "Remove an item");
        api.MapStub("POST", "/fridges/{id:guid}/shopping-list/complete", "CompleteShoppingList", "Remove bought items, optionally moving them to the fridge");
        return api;
    }
}
