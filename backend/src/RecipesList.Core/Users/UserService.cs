using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;

namespace RecipesList.Core.Users;

/// <summary>A cross-recipe product swap preference of the caller (PROJECT §6.6, API.md §1).</summary>
public sealed record ProductPreferenceModel(Guid FromProductId, Guid ToProductId, int Weight);

/// <summary>The signed-in user's own account: profile, nickname, settings and preferences (API.md §1).</summary>
public interface IUserService
{
    /// <summary>The caller; 401 when the session refers to a user that no longer exists.</summary>
    Task<User> GetMeAsync(CancellationToken ct);

    /// <summary>Sets or changes the caller's nickname (400 <c>nickname.invalid</c>, 409 <c>nickname.taken</c>).</summary>
    Task<User> SetNicknameAsync(string nickname, CancellationToken ct);

    /// <summary>Whether the caller could take this nickname: well formed and not used by anyone else.</summary>
    Task<bool> IsNicknameAvailableAsync(string nickname, CancellationToken ct);

    /// <summary>
    /// Replaces the caller's settings. A non-null default fridge must be one the caller belongs to
    /// (404 <c>fridge.not_found</c>); null clears it.
    /// </summary>
    Task<User> UpdateSettingsAsync(LiquidUnits liquidUnits, Guid? defaultFridgeId, CancellationToken ct);

    /// <summary>The caller's product preferences, strongest first within each source product.</summary>
    Task<IReadOnlyList<ProductPreferenceModel>> GetProductPreferencesAsync(CancellationToken ct);
}

/// <inheritdoc />
public sealed class UserService(IAppDbContext db, ICurrentUser currentUser, IUserAccounts accounts, FridgeAccess fridgeAccess)
    : IUserService
{
    /// <inheritdoc />
    public async Task<User> GetMeAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.Id, ct) ?? throw Gone();

    /// <inheritdoc />
    public async Task<User> SetNicknameAsync(string nickname, CancellationToken ct)
    {
        var user = await TrackedMeAsync(ct);
        await accounts.SetNicknameAsync(user, nickname, ct);
        return user;
    }

    /// <inheritdoc />
    public async Task<bool> IsNicknameAvailableAsync(string nickname, CancellationToken ct) =>
        NicknameRules.IsValid(nickname) && !await accounts.IsNicknameTakenAsync(nickname.Trim(), currentUser.Id, ct);

    /// <inheritdoc />
    public async Task<User> UpdateSettingsAsync(LiquidUnits liquidUnits, Guid? defaultFridgeId, CancellationToken ct)
    {
        if (!Enum.IsDefined(liquidUnits))
        {
            throw new DomainValidationException(
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { ["liquidUnits"] = ["invalid"] });
        }

        if (defaultFridgeId is { } fridgeId)
        {
            // The same membership check every fridge operation uses; non-members get 404 fridge.not_found.
            await fridgeAccess.RequireMemberAsync(fridgeId, ct);
        }

        var user = await TrackedMeAsync(ct);
        if (user.LiquidUnits != liquidUnits || user.DefaultFridgeId != defaultFridgeId)
        {
            user.LiquidUnits = liquidUnits;
            user.DefaultFridgeId = defaultFridgeId;
            await db.SaveChangesAsync(ct);
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductPreferenceModel>> GetProductPreferencesAsync(CancellationToken ct)
    {
        var me = currentUser.Id;
        return await db.ProductPreferences
            .Where(p => p.UserId == me)
            .OrderBy(p => p.FromProductId)
            .ThenByDescending(p => p.Weight)
            .ThenBy(p => p.ToProductId)
            .Select(p => new ProductPreferenceModel(p.FromProductId, p.ToProductId, p.Weight))
            .ToListAsync(ct);
    }

    private async Task<User> TrackedMeAsync(CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.Id, ct) ?? throw Gone();

    private static UnauthenticatedException Gone() => new("The session refers to a user that no longer exists.");
}
