using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;

namespace RecipesList.Core.Users;

/// <summary>Who signed in, as reported by Google (or by dev-login).</summary>
public sealed record ExternalLogin(string Subject, string Email, string? AvatarUrl, bool IsAdmin);

/// <summary>
/// Creates or refreshes users on login. Used by dev-login and Google login, so both share one creation path,
/// including the default fridge (API.md §1).
/// </summary>
public interface IUserAccounts
{
    /// <summary>Finds the user by subject or creates them with the default fridge; refreshes email, avatar and admin flag.</summary>
    Task<User> SignInAsync(ExternalLogin login, CancellationToken ct);

    /// <summary>Sets the nickname if valid and free (PROJECT §2); throws 400 <c>nickname.invalid</c> / 409 <c>nickname.taken</c>.</summary>
    Task SetNicknameAsync(User user, string nickname, CancellationToken ct);

    /// <summary>Whether a user other than <paramref name="exceptUserId"/> has a nickname with this normalized form.</summary>
    Task<bool> IsNicknameTakenAsync(string nickname, Guid exceptUserId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class UserAccounts(IAppDbContext db, IFridgeService fridges, TimeProvider clock) : IUserAccounts
{
    /// <inheritdoc />
    public async Task<User> SignInAsync(ExternalLogin login, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(login);

        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == login.Subject, ct);
        if (user is not null)
        {
            await RefreshAsync(user, login, ct);
            return user;
        }

        user = new User
        {
            GoogleSubject = login.Subject,
            Email = login.Email,
            AvatarUrl = login.AvatarUrl,
            IsAdmin = login.IsAdmin,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent first login of the same subject won the unique index: sign in as that user instead.
            db.Users.Entry(user).State = EntityState.Detached;
            var existing = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == login.Subject, ct);
            if (existing is null)
            {
                throw;
            }

            await RefreshAsync(existing, login, ct);
            return existing;
        }

        // users.default_fridge_id and fridges.owner_id reference each other, so the user is saved first. The fridge
        // service then makes "Moja lodówka" the default, because a new user has none (B5's rule).
        await fridges.CreateOwnedAsync(user.Id, UserDefaults.DefaultFridgeName, ct);
        return user;
    }

    /// <inheritdoc />
    public async Task SetNicknameAsync(User user, string nickname, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        var trimmed = NicknameRules.Validate(nickname);
        if (await IsNicknameTakenAsync(trimmed, user.Id, ct))
        {
            throw new ConflictException(ErrorCodes.NicknameTaken);
        }

        if (user.Nickname == trimmed)
        {
            return;
        }

        var (previous, previousNormalized) = (user.Nickname, user.NormalizedNickname);
        user.Nickname = trimmed;
        user.NormalizedNickname = NicknameRules.Normalize(trimmed);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            user.Nickname = previous;
            user.NormalizedNickname = previousNormalized;
            db.Users.Entry(user).State = EntityState.Unchanged;
            if (!await IsNicknameTakenAsync(trimmed, user.Id, ct))
            {
                throw;
            }

            // Someone took it between the check and the save, and the unique index caught it.
            throw new ConflictException(ErrorCodes.NicknameTaken);
        }
    }

    /// <inheritdoc />
    public Task<bool> IsNicknameTakenAsync(string nickname, Guid exceptUserId, CancellationToken ct)
    {
        var normalized = NicknameRules.Normalize(nickname);
        return db.Users.AnyAsync(u => u.NormalizedNickname == normalized && u.Id != exceptUserId, ct);
    }

    private async Task RefreshAsync(User user, ExternalLogin login, CancellationToken ct)
    {
        user.Email = login.Email;
        user.AvatarUrl = login.AvatarUrl ?? user.AvatarUrl;
        user.IsAdmin = login.IsAdmin;
        await db.SaveChangesAsync(ct);
    }
}
