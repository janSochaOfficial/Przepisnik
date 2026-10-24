using Microsoft.AspNetCore.Http.HttpResults;
using RecipesList.Core.Fridges;

namespace RecipesList.Api.Features.Fridges;

/// <summary>Fridges, members, invites, items and deductions (API.md §6, §7).</summary>
internal static class FridgesFeature
{
    public static IServiceCollection AddFridges(this IServiceCollection services)
    {
        services.AddScoped<FridgeAccess>();
        services.AddScoped<IFridgeService, FridgeService>();
        services.AddScoped<IFridgeItemService, FridgeItemService>();
        services.AddScoped<IDeductionService, DeductionService>();
        return services;
    }

    public static IEndpointRouteBuilder MapFridges(this IEndpointRouteBuilder api)
    {
        MapFridgeRoutes(api);
        MapInviteRoutes(api);
        MapItemRoutes(api);
        MapDeductionRoutes(api);
        return api;
    }

    private static void MapFridgeRoutes(IEndpointRouteBuilder api)
    {
        api.MapGet("/fridges", async Task<Ok<FridgeSummaryDto[]>> (IFridgeService fridges, CancellationToken ct) =>
                TypedResults.Ok((await fridges.ListAsync(ct)).Select(FridgeSummaryDto.From).ToArray()))
            .WithName("ListFridges")
            .WithSummary("The caller's fridges");

        api.MapPost("/fridges", async Task<Created<FridgeSummaryDto>> (FridgeNameRequest request, IFridgeService fridges, CancellationToken ct) =>
            {
                var created = await fridges.CreateAsync(request.Name, ct);
                return TypedResults.Created($"/api/v1/fridges/{created.Id}", FridgeSummaryDto.From(created));
            })
            .WithName("CreateFridge")
            .WithSummary("Create a fridge")
            .ProducesValidationProblem();

        api.MapGet("/fridges/{id:guid}", async Task<Ok<FridgeDetailDto>> (Guid id, IFridgeService fridges, CancellationToken ct) =>
                TypedResults.Ok(FridgeDetailDto.From(await fridges.GetAsync(id, ct))))
            .WithName("GetFridge")
            .WithSummary("Get a fridge with its members")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/fridges/{id:guid}", async Task<Ok<FridgeSummaryDto>> (Guid id, FridgeNameRequest request, IFridgeService fridges, CancellationToken ct) =>
                TypedResults.Ok(FridgeSummaryDto.From(await fridges.RenameAsync(id, request.Name, ct))))
            .WithName("RenameFridge")
            .WithSummary("Rename a fridge (owner)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/fridges/{id:guid}", async Task<NoContent> (Guid id, IFridgeService fridges, CancellationToken ct) =>
            {
                await fridges.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteFridge")
            .WithSummary("Delete a fridge (owner)")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/fridges/{id:guid}/members/{userId:guid}", async Task<NoContent> (Guid id, Guid userId, IFridgeService fridges, CancellationToken ct) =>
            {
                await fridges.RemoveMemberAsync(id, userId, ct);
                return TypedResults.NoContent();
            })
            .WithName("RemoveMember")
            .WithSummary("Remove a member or leave a fridge")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapInviteRoutes(IEndpointRouteBuilder api)
    {
        api.MapPost("/fridges/{id:guid}/invites", async Task<Created<InviteDto>> (Guid id, IFridgeService fridges, CancellationToken ct) =>
            {
                var invite = InviteDto.From(await fridges.CreateInviteAsync(id, ct));
                return TypedResults.Created($"/api/v1/invites/{invite.Token}", invite);
            })
            .WithName("CreateInvite")
            .WithSummary("Create an invite link (owner)")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/fridges/{id:guid}/invites", async Task<Ok<InviteDto[]>> (Guid id, IFridgeService fridges, CancellationToken ct) =>
                TypedResults.Ok((await fridges.ListInvitesAsync(id, ct)).Select(InviteDto.From).ToArray()))
            .WithName("ListInvites")
            .WithSummary("Active invite links (owner)")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/fridges/{id:guid}/invites/{inviteId:guid}", async Task<NoContent> (Guid id, Guid inviteId, IFridgeService fridges, CancellationToken ct) =>
            {
                await fridges.RevokeInviteAsync(id, inviteId, ct);
                return TypedResults.NoContent();
            })
            .WithName("RevokeInvite")
            .WithSummary("Revoke an invite link (owner)")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/invites/{token}", async Task<Ok<InvitePreviewDto>> (string token, IFridgeService fridges, CancellationToken ct) =>
                TypedResults.Ok(InvitePreviewDto.From(await fridges.PreviewInviteAsync(token, ct))))
            .WithName("PreviewInvite")
            .WithSummary("Preview an invite")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/invites/{token}/accept", async Task<Ok<FridgeSummaryDto>> (string token, IFridgeService fridges, CancellationToken ct) =>
                TypedResults.Ok(FridgeSummaryDto.From(await fridges.AcceptInviteAsync(token, ct))))
            .WithName("AcceptInvite")
            .WithSummary("Join a fridge through an invite")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapItemRoutes(IEndpointRouteBuilder api)
    {
        api.MapGet("/fridges/{id:guid}/items", async Task<Ok<FridgeItemDto[]>> (Guid id, IFridgeItemService items, CancellationToken ct) =>
                TypedResults.Ok((await items.ListAsync(id, ct)).Select(FridgeItemDto.From).ToArray()))
            .WithName("ListFridgeItems")
            .WithSummary("Items in a fridge")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/fridges/{id:guid}/items", async Task<Created<FridgeItemDto>> (Guid id, FridgeItemWriteRequest request, IFridgeItemService items, CancellationToken ct) =>
            {
                var item = FridgeItemDto.From(await items.CreateAsync(id, request.ToInput(), ct));
                return TypedResults.Created($"/api/v1/fridges/{id}/items/{item.Id}", item);
            })
            .WithName("CreateFridgeItem")
            .WithSummary("Add an item to a fridge")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/fridges/{id:guid}/items/{itemId:guid}", async Task<Results<Ok<FridgeItemDto>, NoContent>> (
                Guid id, Guid itemId, FridgeItemUpdateRequest request, IFridgeItemService items, CancellationToken ct) =>
                await items.UpdateAsync(id, itemId, request.ToInput(), request.Version, ct) is { } updated
                    ? TypedResults.Ok(FridgeItemDto.From(updated))
                    : TypedResults.NoContent())
            .WithName("UpdateFridgeItem")
            .WithSummary("Edit a fridge item")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/fridges/{id:guid}/items/{itemId:guid}", async Task<NoContent> (Guid id, Guid itemId, IFridgeItemService items, CancellationToken ct) =>
            {
                await items.DeleteAsync(id, itemId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteFridgeItem")
            .WithSummary("Remove a fridge item")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapDeductionRoutes(IEndpointRouteBuilder api)
    {
        api.MapPost("/fridges/{id:guid}/deductions", async Task<Created<DeductionResultDto>> (Guid id, DeductionRequest request, IDeductionService deductions, CancellationToken ct) =>
            {
                var result = DeductionResultDto.From(await deductions.DeductAsync(id, request.ToInput(), ct));
                return TypedResults.Created($"/api/v1/fridges/{id}/deductions/{result.Id}", result);
            })
            .WithName("CreateDeduction")
            .WithSummary("Deduct a cooked recipe from a fridge")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/fridges/{id:guid}/deductions/{deductionId:guid}/undo", async Task<Ok<DeductionResultDto>> (Guid id, Guid deductionId, IDeductionService deductions, CancellationToken ct) =>
                TypedResults.Ok(DeductionResultDto.From(await deductions.UndoAsync(id, deductionId, ct))))
            .WithName("UndoDeduction")
            .WithSummary("Undo a deduction")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
