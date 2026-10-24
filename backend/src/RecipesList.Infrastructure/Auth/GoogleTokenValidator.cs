using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace RecipesList.Infrastructure.Auth;

/// <summary>What a valid Google ID token says about the person (TECHNICAL §3.5).</summary>
/// <param name="Subject">Google's stable account id (<c>sub</c>).</param>
/// <param name="Email">The account email.</param>
/// <param name="EmailVerified">Whether Google verified the email (<c>email_verified</c>).</param>
/// <param name="PictureUrl">The avatar URL, if any.</param>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? PictureUrl);

/// <summary>Validates Google ID tokens. Tests replace it with a fake.</summary>
public interface IGoogleTokenValidator
{
    /// <summary>The identity in <paramref name="idToken"/>, or null when the token is invalid, expired or for another audience.</summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct);
}

/// <summary>Google sign-in settings (<c>Auth__Google__ClientId</c>).</summary>
public sealed class GoogleAuthOptions
{
    /// <summary>The OAuth web client id the token's audience must match; empty disables Google sign-in.</summary>
    public string? ClientId { get; set; }
}

/// <summary><see cref="IGoogleTokenValidator"/> backed by <see cref="GoogleJsonWebSignature.ValidateAsync(string, GoogleJsonWebSignature.ValidationSettings)"/>.</summary>
public sealed class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    /// <summary>Allowed clock skew between Google and this server.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            // Checks the signature against Google's certificates, the issuer, the expiry and the audience.
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
                IssuedAtClockTolerance = ClockTolerance,
                ExpirationTimeClockTolerance = ClockTolerance,
            });
        }
        catch (InvalidJwtException)
        {
            return null;
        }

        return string.IsNullOrEmpty(payload.Subject) || string.IsNullOrEmpty(payload.Email)
            ? null
            : new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Picture);
    }
}
