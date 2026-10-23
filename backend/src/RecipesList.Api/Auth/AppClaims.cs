using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using RecipesList.Core.Users;

namespace RecipesList.Api.Auth;

/// <summary>Claims carried in the session cookie and how a user is signed in.</summary>
internal static class AppClaims
{
    public const string UserId = ClaimTypes.NameIdentifier;
    public const string Admin = "rl:admin";

    /// <summary>"true" once the user has a nickname; lets the onboarding gate skip a database lookup.</summary>
    public const string Onboarded = "rl:onboarded";

    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var claims = new List<Claim>
        {
            new(UserId, user.Id.ToString()),
            new(Admin, user.IsAdmin ? "true" : "false"),
            new(Onboarded, user.Nickname is null ? "false" : "true"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>Issues (or re-issues, e.g. after onboarding) the persistent 30-day session cookie.</summary>
    public static Task SignInAsync(HttpContext context, User user) =>
        context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(user),
            new AuthenticationProperties { IsPersistent = true });
}
