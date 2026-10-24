using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Fridges;

/// <summary>Fridges, members and invite links (API.md §6, PROJECT §8.1).</summary>
public interface IFridgeService
{
    /// <summary>The fridges the caller owns or belongs to, oldest first, in one statement whatever the count.</summary>
    Task<IReadOnlyList<FridgeSummaryModel>> ListAsync(CancellationToken ct);

    /// <summary>A fridge with its members (404 <c>fridge.not_found</c> for non-members).</summary>
    Task<FridgeDetailModel> GetAsync(Guid fridgeId, CancellationToken ct);

    /// <summary>Creates a fridge owned by the caller.</summary>
    Task<FridgeSummaryModel> CreateAsync(string name, CancellationToken ct);

    /// <summary>
    /// Creates a fridge owned by <paramref name="ownerId"/>, who adopts it as the default when they have none, and returns
    /// its id. For callers without a session yet (the first sign-in, B1); everything else uses <see cref="CreateAsync"/>.
    /// </summary>
    Task<Guid> CreateOwnedAsync(Guid ownerId, string name, CancellationToken ct);

    /// <summary>Renames a fridge (owner).</summary>
    Task<FridgeSummaryModel> RenameAsync(Guid fridgeId, string name, CancellationToken ct);

    /// <summary>Deletes a fridge with its items, invites, deductions and shopping list (owner).</summary>
    Task DeleteAsync(Guid fridgeId, CancellationToken ct);

    /// <summary>Creates an invite link valid for <see cref="FridgeService.InviteLifetime"/> (owner).</summary>
    Task<InviteModel> CreateInviteAsync(Guid fridgeId, CancellationToken ct);

    /// <summary>Invites that are neither expired nor revoked, newest first (owner).</summary>
    Task<IReadOnlyList<InviteModel>> ListInvitesAsync(Guid fridgeId, CancellationToken ct);

    /// <summary>Revokes an invite (owner). Revoking twice is a no-op.</summary>
    Task RevokeInviteAsync(Guid fridgeId, Guid inviteId, CancellationToken ct);

    /// <summary>What an active invite is for; 404 <c>invite.not_found</c> when unknown, expired or revoked.</summary>
    Task<InvitePreviewModel> PreviewInviteAsync(string token, CancellationToken ct);

    /// <summary>Joins the fridge of an active invite; idempotent for existing members.</summary>
    Task<FridgeSummaryModel> AcceptInviteAsync(string token, CancellationToken ct);

    /// <summary>The owner removes a member, or a member leaves (400 <c>fridge.owner_cannot_leave</c> for the owner).</summary>
    Task RemoveMemberAsync(Guid fridgeId, Guid userId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class FridgeService(IAppDbContext db, ICurrentUser currentUser, FridgeAccess access, TimeProvider time) : IFridgeService
{
    /// <summary>How long an invite link stays valid (D-08).</summary>
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    /// <summary>Random bytes in an invite token; URL-safe base64 gives 43 characters.</summary>
    public const int InviteTokenBytes = 32;

    public const int MaxNameLength = 80;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FridgeSummaryModel>> ListAsync(CancellationToken ct) =>
        await Summaries(currentUser.Id).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<FridgeDetailModel> GetAsync(Guid fridgeId, CancellationToken ct)
    {
        var summary = await GetSummaryAsync(fridgeId, ct);
        var members = await db.FridgeMembers
            .Where(m => m.FridgeId == fridgeId)
            .Select(m => new { m.UserId, Nickname = m.User!.Nickname ?? "", m.Role, m.JoinedAt })
            .ToListAsync(ct);

        return new FridgeDetailModel(
            summary,
            [.. members
                .OrderBy(m => m.Role == FridgeRole.Owner ? 0 : 1)
                .ThenBy(m => m.JoinedAt)
                .ThenBy(m => m.UserId)
                .Select(m => new FridgeMemberModel(m.UserId, m.Nickname, m.Role))]);
    }

    /// <inheritdoc />
    public async Task<FridgeSummaryModel> CreateAsync(string name, CancellationToken ct)
    {
        var fridgeId = await CreateOwnedAsync(currentUser.Id, name, ct);
        return await GetSummaryAsync(fridgeId, ct);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateOwnedAsync(Guid ownerId, string name, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var fridge = new Fridge { Name = ValidName(name), OwnerId = ownerId, CreatedAt = now };
        fridge.Members.Add(new FridgeMember { FridgeId = fridge.Id, UserId = ownerId, Role = FridgeRole.Owner, JoinedAt = now });
        db.Fridges.Add(fridge);
        await db.SaveChangesAsync(ct);
        await AdoptAsDefaultIfNoneAsync(ownerId, fridge.Id, ct);
        return fridge.Id;
    }

    /// <inheritdoc />
    public async Task<FridgeSummaryModel> RenameAsync(Guid fridgeId, string name, CancellationToken ct)
    {
        await access.RequireOwnerAsync(fridgeId, ct);
        var validName = ValidName(name);
        var fridge = await db.Fridges.SingleAsync(f => f.Id == fridgeId, ct);
        if (fridge.Name != validName)
        {
            fridge.Name = validName;
            await db.SaveChangesAsync(ct);
        }

        return await GetSummaryAsync(fridgeId, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid fridgeId, CancellationToken ct)
    {
        await access.RequireOwnerAsync(fridgeId, ct);
        var fridge = await db.Fridges.SingleAsync(f => f.Id == fridgeId, ct);
        var affected = await db.Users.Where(u => u.DefaultFridgeId == fridgeId).Select(u => u.Id).ToListAsync(ct);
        await ResetDefaultFridgeAsync(affected, fridgeId, ct);

        // Members, invites, items, deductions and shopping-list rows go with it (ON DELETE CASCADE).
        db.Fridges.Remove(fridge);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<InviteModel> CreateInviteAsync(Guid fridgeId, CancellationToken ct)
    {
        await access.RequireOwnerAsync(fridgeId, ct);
        var now = time.GetUtcNow();
        var invite = new FridgeInvite
        {
            FridgeId = fridgeId,
            Token = NewToken(),
            CreatedById = currentUser.Id,
            CreatedAt = now,
            ExpiresAt = now + InviteLifetime,
        };
        db.FridgeInvites.Add(invite);
        await db.SaveChangesAsync(ct);
        return new InviteModel(invite.Id, invite.Token, invite.ExpiresAt, invite.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InviteModel>> ListInvitesAsync(Guid fridgeId, CancellationToken ct)
    {
        await access.RequireOwnerAsync(fridgeId, ct);
        var now = time.GetUtcNow();
        return await db.FridgeInvites
            .Where(i => i.FridgeId == fridgeId && i.RevokedAt == null && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Select(i => new InviteModel(i.Id, i.Token, i.ExpiresAt, i.CreatedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task RevokeInviteAsync(Guid fridgeId, Guid inviteId, CancellationToken ct)
    {
        await access.RequireOwnerAsync(fridgeId, ct);
        var invite = await db.FridgeInvites.SingleOrDefaultAsync(i => i.Id == inviteId && i.FridgeId == fridgeId, ct)
            ?? throw new NotFoundException(ErrorCodes.InviteNotFound);
        if (invite.RevokedAt is null)
        {
            invite.RevokedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<InvitePreviewModel> PreviewInviteAsync(string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);
        var me = currentUser.Id;
        var now = time.GetUtcNow();
        var fridges = db.Fridges;
        var preview = await db.FridgeInvites
            .Where(i => i.Token == token && i.RevokedAt == null && i.ExpiresAt > now)
            .SelectMany(i => fridges.Where(f => f.Id == i.FridgeId), (i, f) => new InvitePreviewModel(
                f.Name,
                new FridgeUserRef(f.OwnerId, f.Owner!.Nickname ?? ""),
                i.ExpiresAt,
                f.Members.Any(m => m.UserId == me)))
            .SingleOrDefaultAsync(ct);
        return preview ?? throw new NotFoundException(ErrorCodes.InviteNotFound);
    }

    /// <inheritdoc />
    public async Task<FridgeSummaryModel> AcceptInviteAsync(string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);
        var me = currentUser.Id;
        var now = time.GetUtcNow();
        var fridgeId = await db.FridgeInvites
            .Where(i => i.Token == token && i.RevokedAt == null && i.ExpiresAt > now)
            .Select(i => (Guid?)i.FridgeId)
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.InviteNotFound);

        var isMember = await db.FridgeMembers.AnyAsync(m => m.FridgeId == fridgeId && m.UserId == me, ct);
        if (!isMember)
        {
            var member = new FridgeMember { FridgeId = fridgeId, UserId = me, Role = FridgeRole.Member, JoinedAt = now };
            db.FridgeMembers.Add(member);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // A simultaneous accept by the same user won the primary key: accepting stays idempotent.
                db.FridgeMembers.Entry(member).State = EntityState.Detached;
                if (!await db.FridgeMembers.AnyAsync(m => m.FridgeId == fridgeId && m.UserId == me, ct))
                {
                    throw;
                }

                return await GetSummaryAsync(fridgeId, ct);
            }

            await AdoptAsDefaultIfNoneAsync(me, fridgeId, ct);
        }

        return await GetSummaryAsync(fridgeId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(Guid fridgeId, Guid userId, CancellationToken ct)
    {
        var me = currentUser.Id;
        var role = await access.RequireMemberAsync(fridgeId, ct);
        if (userId == me)
        {
            if (role == FridgeRole.Owner)
            {
                throw new DomainValidationException(ErrorCodes.FridgeOwnerCannotLeave);
            }
        }
        else if (role != FridgeRole.Owner)
        {
            throw new ForbiddenException(ErrorCodes.FridgeOwnerRequired);
        }

        var member = await db.FridgeMembers.SingleOrDefaultAsync(m => m.FridgeId == fridgeId && m.UserId == userId, ct);
        if (member is null)
        {
            // Already gone: DELETE is idempotent, and the caller (the owner) learns nothing new.
            return;
        }

        db.FridgeMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        await ResetDefaultFridgeAsync([userId], fridgeId, ct);
    }

    private IQueryable<FridgeSummaryModel> Summaries(Guid me, Guid? fridgeId = null)
    {
        var items = db.FridgeItems;
        var fridges = fridgeId is { } id ? db.Fridges.Where(f => f.Id == id) : db.Fridges;
        return fridges
            .Where(f => f.Members.Any(m => m.UserId == me))
            .OrderBy(f => f.CreatedAt)
            .ThenBy(f => f.Id)
            .Select(f => new FridgeSummaryModel(
                f.Id,
                f.Name,
                f.Members.Where(m => m.UserId == me).Select(m => m.Role).First(),
                new FridgeUserRef(f.OwnerId, f.Owner!.Nickname ?? ""),
                f.Members.Count,
                items.Count(i => i.FridgeId == f.Id)));
    }

    private async Task<FridgeSummaryModel> GetSummaryAsync(Guid fridgeId, CancellationToken ct) =>
        await Summaries(currentUser.Id, fridgeId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.FridgeNotFound);

    /// <summary>Points users whose default was <paramref name="fridgeId"/> at another fridge of theirs, or at none.</summary>
    private async Task ResetDefaultFridgeAsync(List<Guid> userIds, Guid fridgeId, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var users = await db.Users.Where(u => userIds.Contains(u.Id) && u.DefaultFridgeId == fridgeId).ToListAsync(ct);
        if (users.Count == 0)
        {
            return;
        }

        var ids = users.Select(u => u.Id).ToList();
        var remaining = await db.FridgeMembers
            .Where(m => ids.Contains(m.UserId) && m.FridgeId != fridgeId)
            .Select(m => new { m.UserId, m.FridgeId, m.Role, m.JoinedAt })
            .ToListAsync(ct);

        foreach (var user in users)
        {
            user.DefaultFridgeId = remaining
                .Where(m => m.UserId == user.Id)
                .OrderBy(m => m.Role == FridgeRole.Owner ? 0 : 1)
                .ThenBy(m => m.JoinedAt)
                .ThenBy(m => m.FridgeId)
                .Select(m => (Guid?)m.FridgeId)
                .FirstOrDefault();
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>A user without a default fridge (they left or lost all of them) adopts the one they just got.</summary>
    private async Task AdoptAsDefaultIfNoneAsync(Guid userId, Guid fridgeId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (user.DefaultFridgeId is null)
        {
            user.DefaultFridgeId = fridgeId;
            await db.SaveChangesAsync(ct);
        }
    }

    private static string ValidName(string name)
    {
        var trimmed = name?.Trim() ?? "";
        var errors = new ValidationErrors();
        if (trimmed.Length == 0)
        {
            errors.Add("name", "required");
        }
        else if (trimmed.Length > MaxNameLength)
        {
            errors.Add("name", "too_long");
        }

        errors.ThrowIfAny();
        return trimmed;
    }

    private static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(InviteTokenBytes));
}
