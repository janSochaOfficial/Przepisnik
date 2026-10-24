using System.ComponentModel.DataAnnotations;

namespace RecipesList.Api.Features.Auth;

/// <summary>Public sign-in configuration for the login page (API.md §1).</summary>
public sealed record AuthConfigResponse(string? GoogleClientId, bool DevLoginEnabled);

/// <summary>Google sign-in payload: the ID token from Google Identity Services (TECHNICAL §3.5).</summary>
public sealed record GoogleLoginRequest
{
    /// <summary>The Google ID token (a JWT) whose audience is this app's client id.</summary>
    [Required]
    public required string IdToken { get; init; }
}

/// <summary>Dev-login payload; only available in Development and Testing (TECHNICAL §3.5).</summary>
public sealed record DevLoginRequest
{
    /// <summary>Identifies the dev user; the same email always signs in as the same user.</summary>
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    /// <summary>Optional nickname, set if the user has none yet (skips onboarding). Checked like <c>PUT /me/nickname</c>.</summary>
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
