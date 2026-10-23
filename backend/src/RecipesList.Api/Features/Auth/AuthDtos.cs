using System.ComponentModel.DataAnnotations;
using RecipesList.Core.Users;

namespace RecipesList.Api.Features.Auth;

/// <summary>Per-user settings (API.md §1 <c>MeSettings</c>).</summary>
public sealed record MeSettingsResponse(LiquidUnits LiquidUnits, Guid? DefaultFridgeId);

/// <summary>The signed-in user (API.md §1 <c>Me</c>). A null nickname means onboarding is required.</summary>
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

/// <summary>Public sign-in configuration for the login page (API.md §1).</summary>
public sealed record AuthConfigResponse(string? GoogleClientId, bool DevLoginEnabled);

/// <summary>Dev-login payload; only available in Development and Testing (TECHNICAL §3.5).</summary>
public sealed record DevLoginRequest
{
    /// <summary>Identifies the dev user; the same email always signs in as the same user.</summary>
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    /// <summary>Optional nickname, set if the user has none yet (skips onboarding).</summary>
    [MaxLength(30)]
    public string? Nickname { get; init; }
}

/// <summary>Configuration read by the auth feature.</summary>
public sealed class AuthOptions
{
    /// <summary>Google OAuth web client id (<c>Auth__Google__ClientId</c>); empty means Google sign-in is off.</summary>
    public string? GoogleClientId { get; set; }

    /// <summary>Emails that get the admin flag (<c>Admin__Emails__0</c>, …).</summary>
    public IReadOnlyList<string> AdminEmails { get; set; } = [];

    public bool IsAdmin(string email) => AdminEmails.Any(a => string.Equals(a.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}
