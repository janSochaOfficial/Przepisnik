using System.ComponentModel.DataAnnotations;
using RecipesList.Core.Users;

namespace RecipesList.Api.Features.Users;

/// <summary>Per-user settings (API.md §1 <c>MeSettings</c>).</summary>
public sealed record MeSettingsResponse(LiquidUnits LiquidUnits, Guid? DefaultFridgeId);

/// <summary>The signed-in user (API.md §1 <c>Me</c>). A null nickname means onboarding is required.</summary>
/// <remarks>Only ever returned to the user themselves: the email is never shown to anyone else (PROJECT §2).</remarks>
public sealed record MeResponse(Guid Id, string Email, string? Nickname, string? AvatarUrl, bool IsAdmin, MeSettingsResponse Settings)
{
    public static MeResponse From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new MeResponse(
            user.Id,
            user.Email,
            user.Nickname,
            user.AvatarUrl,
            user.IsAdmin,
            new MeSettingsResponse(user.LiquidUnits, user.DefaultFridgeId));
    }
}

/// <summary>Payload of <c>PUT /me/nickname</c> (<c>NicknameWrite</c>). The rules are checked by the service, so a
/// malformed nickname answers 400 <c>nickname.invalid</c> rather than a generic validation error.</summary>
public sealed record NicknameRequest
{
    /// <summary>3–30 letters (Polish letters included), digits, <c>_</c> or <c>-</c>; trimmed.</summary>
    [Required]
    public required string Nickname { get; init; }
}

/// <summary>Answer of <c>GET /users/nickname-available</c>.</summary>
public sealed record NicknameAvailabilityResponse(bool Available);

/// <summary>Payload of <c>PUT /me/settings</c> (<c>MeSettings</c>); replaces both settings.</summary>
public sealed record MeSettingsRequest
{
    /// <summary>How liquid amounts are shown (PROJECT §5.4).</summary>
    public required LiquidUnits LiquidUnits { get; init; }

    /// <summary>A fridge the user belongs to, or null for none.</summary>
    public Guid? DefaultFridgeId { get; init; }
}

/// <summary>A cross-recipe product preference (API.md §1 <c>ProductPreference</c>).</summary>
public sealed record ProductPreferenceResponse(Guid FromProductId, Guid ToProductId, int Weight)
{
    public static ProductPreferenceResponse From(ProductPreferenceModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new ProductPreferenceResponse(model.FromProductId, model.ToProductId, model.Weight);
    }
}
