using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;

namespace RecipesList.Core.Fridges;

/// <summary>
/// The fridge membership checks every fridge, item, deduction and shopping-list operation starts with (PROJECT §8.1).
/// A non-member gets 404 <c>fridge.not_found</c>, so the fridge's existence does not leak (TECHNICAL §3.5).
/// </summary>
public sealed class FridgeAccess(IAppDbContext db, ICurrentUser currentUser)
{
    /// <summary>The caller's role in the fridge; throws 404 <c>fridge.not_found</c> when they are not a member.</summary>
    public async Task<FridgeRole> RequireMemberAsync(Guid fridgeId, CancellationToken ct)
    {
        var me = currentUser.Id;
        var roles = await db.FridgeMembers
            .Where(m => m.FridgeId == fridgeId && m.UserId == me)
            .Select(m => (FridgeRole?)m.Role)
            .ToListAsync(ct);
        return roles is [{ } role] ? role : throw new NotFoundException(ErrorCodes.FridgeNotFound);
    }

    /// <summary>Like <see cref="RequireMemberAsync"/>, and throws 403 <c>fridge.owner_required</c> for a plain member.</summary>
    public async Task RequireOwnerAsync(Guid fridgeId, CancellationToken ct)
    {
        if (await RequireMemberAsync(fridgeId, ct) != FridgeRole.Owner)
        {
            throw new ForbiddenException(ErrorCodes.FridgeOwnerRequired);
        }
    }
}
