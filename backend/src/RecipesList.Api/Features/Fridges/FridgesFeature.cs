namespace RecipesList.Api.Features.Fridges;

/// <summary>Fridges, members, invites, items and deductions (API.md §6, §7). Implemented in B5.</summary>
internal static class FridgesFeature
{
    public static IServiceCollection AddFridges(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapFridges(this IEndpointRouteBuilder api)
    {
        api.MapStub("GET", "/fridges", "ListFridges", "The caller's fridges");
        api.MapStub("POST", "/fridges", "CreateFridge", "Create a fridge");
        api.MapStub("GET", "/fridges/{id:guid}", "GetFridge", "Get a fridge with its members");
        api.MapStub("PUT", "/fridges/{id:guid}", "RenameFridge", "Rename a fridge (owner)");
        api.MapStub("DELETE", "/fridges/{id:guid}", "DeleteFridge", "Delete a fridge (owner)");

        api.MapStub("POST", "/fridges/{id:guid}/invites", "CreateInvite", "Create an invite link (owner)");
        api.MapStub("GET", "/fridges/{id:guid}/invites", "ListInvites", "Active invite links (owner)");
        api.MapStub("DELETE", "/fridges/{id:guid}/invites/{inviteId:guid}", "RevokeInvite", "Revoke an invite link (owner)");
        api.MapStub("GET", "/invites/{token}", "PreviewInvite", "Preview an invite");
        api.MapStub("POST", "/invites/{token}/accept", "AcceptInvite", "Join a fridge through an invite");
        api.MapStub("DELETE", "/fridges/{id:guid}/members/{userId:guid}", "RemoveMember", "Remove a member or leave a fridge");

        api.MapStub("GET", "/fridges/{id:guid}/items", "ListFridgeItems", "Items in a fridge");
        api.MapStub("POST", "/fridges/{id:guid}/items", "CreateFridgeItem", "Add an item to a fridge");
        api.MapStub("PUT", "/fridges/{id:guid}/items/{itemId:guid}", "UpdateFridgeItem", "Edit a fridge item");
        api.MapStub("DELETE", "/fridges/{id:guid}/items/{itemId:guid}", "DeleteFridgeItem", "Remove a fridge item");

        api.MapStub("POST", "/fridges/{id:guid}/deductions", "CreateDeduction", "Deduct a cooked recipe from a fridge");
        api.MapStub("POST", "/fridges/{id:guid}/deductions/{deductionId:guid}/undo", "UndoDeduction", "Undo a deduction");
        return api;
    }
}
