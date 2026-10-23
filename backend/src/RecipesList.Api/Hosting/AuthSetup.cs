using Microsoft.AspNetCore.Authentication.Cookies;
using RecipesList.Api.Auth;
using RecipesList.Core.Common;

namespace RecipesList.Api.Hosting;

/// <summary>Cookie authentication (TECHNICAL §3.5): 30-day sliding session, 401/403 instead of redirects.</summary>
internal static class AuthSetup
{
    /// <summary>The production cookie name. The <c>__Host-</c> prefix requires <c>Secure</c>, <c>Path=/</c> and no domain.</summary>
    public const string SecureCookieName = "__Host-rl_session";

    /// <summary>Development over plain http cannot use the <c>__Host-</c> prefix, so it uses this name.</summary>
    public const string DevelopmentCookieName = "rl_session";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        var relaxed = environment.IsDevelopment();

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = relaxed ? DevelopmentCookieName : SecureCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.Cookie.SecurePolicy = relaxed ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;

                // The API never redirects; status code pages turn these into ProblemDetails.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<OnboardingGateFilter>();
        return services;
    }
}
