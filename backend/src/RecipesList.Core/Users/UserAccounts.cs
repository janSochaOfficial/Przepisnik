using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;

namespace RecipesList.Core.Users;

/// <summary>Who signed in, as reported by Google (or by dev-login).</summary>
public sealed record ExternalLogin(string Subject, string Email, string? AvatarUrl, bool IsAdmin);

/// <summary>
/// Creates or refreshes users on login. Used by dev-login (T00) and Google login (B1), so both share one creation path,
/// including the default fridge (API.md §1).
/// </summary>
public interface IUserAccounts
{
    /// <summary>Finds the user by subject or creates them with the default fridge; refreshes email, avatar and admin flag.</summary>
    Task<User> SignInAsync(ExternalLogin login, CancellationToken ct);

    /// <summary>Sets the nickname if valid and free (PROJECT §2); throws 400 <c>nickname.invalid</c> / 409 <c>nickname.taken</c>.</summary>
    Task SetNicknameAsync(User user, string nickname, CancellationToken ct);
}

/// <inheritdoc />
public sealed partial class UserAccounts(IAppDbContext db, TimeProvider clock) : IUserAccounts
{
    /// <inheritdoc />
    public async Task<User> SignInAsync(ExternalLogin login, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(login);

        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == login.Subject, ct);
        var now = clock.GetUtcNow();

        if (user is not null)
        {
            user.Email = login.Email;
            user.AvatarUrl = login.AvatarUrl ?? user.AvatarUrl;
            user.IsAdmin = login.IsAdmin;
            await db.SaveChangesAsync(ct);
            return user;
        }

        user = new User
        {
            GoogleSubject = login.Subject,
            Email = login.Email,
            AvatarUrl = login.AvatarUrl,
            IsAdmin = login.IsAdmin,
            CreatedAt = now,
        };
        var fridge = new Fridge { Name = UserDefaults.DefaultFridgeName, OwnerId = user.Id, CreatedAt = now };
        fridge.Members.Add(new FridgeMember { FridgeId = fridge.Id, UserId = user.Id, Role = FridgeRole.Owner, JoinedAt = now });

        // users.default_fridge_id and fridges.owner_id reference each other, so the default is set in a second step.
        db.Users.Add(user);
        db.Fridges.Add(fridge);
        await db.SaveChangesAsync(ct);
        user.DefaultFridgeId = fridge.Id;
        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <inheritdoc />
    public async Task SetNicknameAsync(User user, string nickname, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(nickname);

        var trimmed = nickname.Trim();
        if (!IsValidNickname(trimmed))
        {
            throw new DomainValidationException(
                ErrorCodes.NicknameInvalid,
                new Dictionary<string, string[]> { ["nickname"] = [ErrorCodes.NicknameInvalid] });
        }

        var normalized = NameNormalizer.Normalize(trimmed);
        var taken = await db.Users.AnyAsync(u => u.NormalizedNickname == normalized && u.Id != user.Id, ct);
        if (taken)
        {
            throw new ConflictException(ErrorCodes.NicknameTaken);
        }

        user.Nickname = trimmed;
        user.NormalizedNickname = normalized;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>3–30 characters: letters (Polish letters included), digits, <c>_</c> and <c>-</c>.</summary>
    public static bool IsValidNickname(string nickname) => NicknamePattern().IsMatch(nickname);

    [GeneratedRegex(@"^[\p{L}\p{Nd}_-]{3,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex NicknamePattern();
}
