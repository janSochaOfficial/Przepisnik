using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecipesList.Api.Auth;
using RecipesList.Api.Hosting;
using RecipesList.Core.Common;
using RecipesList.Core.Users;

namespace RecipesList.Api.Features.Auth;

/// <summary>
/// Auth and current user (API.md §1). T00 implements dev-login, <c>/auth/config</c>, <c>/auth/logout</c> and a minimal
/// <c>GET /me</c> so that the app works end to end; B1 implements the rest.
/// </summary>
internal static class AuthFeature
{
    public static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.GoogleClientId = configuration["Auth:Google:ClientId"];
            options.AdminEmails = configuration.GetSection("Admin:Emails").Get<string[]>() ?? [];
        });
        services.AddScoped<IUserAccounts, UserAccounts>();
        return services;
    }

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder api)
    {
        var env = api.ServiceProvider.GetRequiredService<IHostEnvironment>();

        api.MapStub("POST", "/auth/google", "AuthGoogle", "Sign in with a Google ID token")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        if (IsDevLoginEnabled(env))
        {
            api.MapPost("/auth/dev-login", DevLoginAsync)
                .WithName("AuthDevLogin")
                .WithSummary("Sign in without Google (Development and Testing only)")
                .AllowAnonymous()
                .RequireRateLimiting(RateLimitPolicies.Auth);
        }

        api.MapGet("/auth/config", GetConfig)
            .WithName("GetAuthConfig")
            .WithSummary("Public sign-in configuration")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        api.MapPost("/auth/logout", LogoutAsync)
            .WithName("AuthLogout")
            .WithSummary("End the session")
            .AllowWithoutNickname()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        api.MapGet("/me", GetMeAsync)
            .WithName("GetMe")
            .WithSummary("The signed-in user")
            .AllowWithoutNickname();

        api.MapStub("PUT", "/me/nickname", "SetNickname", "Set or change the nickname").AllowWithoutNickname();
        api.MapStub("GET", "/users/nickname-available", "IsNicknameAvailable", "Check whether a nickname is free").AllowWithoutNickname();
        api.MapStub("PUT", "/me/settings", "UpdateSettings", "Update the per-user settings").AllowWithoutNickname();
        api.MapStub("GET", "/me/product-preferences", "GetProductPreferences", "The user's product swap preferences").AllowWithoutNickname();
        return api;
    }

    internal static bool IsDevLoginEnabled(IHostEnvironment env) => env.IsDevelopment() || env.IsEnvironment("Testing");

    private static Ok<AuthConfigResponse> GetConfig(IOptions<AuthOptions> options, IHostEnvironment env)
    {
        var clientId = string.IsNullOrWhiteSpace(options.Value.GoogleClientId) ? null : options.Value.GoogleClientId;
        return TypedResults.Ok(new AuthConfigResponse(clientId, IsDevLoginEnabled(env)));
    }

    private static async Task<Ok<MeResponse>> DevLoginAsync(
        DevLoginRequest request,
        IUserAccounts accounts,
        IOptions<AuthOptions> options,
        HttpContext http,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await accounts.SignInAsync(
            new ExternalLogin($"dev|{email}", email, AvatarUrl: null, IsAdmin: options.Value.IsAdmin(email)),
            ct);

        if (user.Nickname is null && !string.IsNullOrWhiteSpace(request.Nickname))
        {
            await accounts.SetNicknameAsync(user, request.Nickname, ct);
        }

        await AppClaims.SignInAsync(http, user);
        return TypedResults.Ok(MeResponse.From(user));
    }

    private static async Task<NoContent> LogoutAsync(HttpContext http, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<MeResponse>> GetMeAsync(ICurrentUser currentUser, IAppDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.Id, ct)
            ?? throw new UnauthenticatedException("The session refers to a user that no longer exists.");
        return TypedResults.Ok(MeResponse.From(user));
    }
}
