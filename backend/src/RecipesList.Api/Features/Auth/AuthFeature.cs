using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using RecipesList.Api.Auth;
using RecipesList.Api.Features.Users;
using RecipesList.Api.Hosting;
using RecipesList.Api.Middleware;
using RecipesList.Core.Common;
using RecipesList.Core.Users;
using RecipesList.Infrastructure.Auth;

namespace RecipesList.Api.Features.Auth;

/// <summary>
/// Auth and current user (API.md §1): Google sign-in, dev-login, logout and the public config. The current-user
/// endpoints (<c>/me*</c>, <c>/users/nickname-available</c>) live in <see cref="UsersEndpoints"/> and are mapped from here.
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
        services.AddOptions<GoogleAuthOptions>().Configure<IOptions<AuthOptions>>((google, auth) =>
            google.ClientId = auth.Value.GoogleClientId);
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddUsers();
        return services;
    }

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder api)
    {
        var env = api.ServiceProvider.GetRequiredService<IHostEnvironment>();

        api.MapPost("/auth/google", GoogleLoginAsync)
            .WithName("AuthGoogle")
            .WithSummary("Sign in with a Google ID token")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        if (IsDevLoginEnabled(env))
        {
            api.MapPost("/auth/dev-login", DevLoginAsync)
                .WithName("AuthDevLogin")
                .WithSummary("Sign in without Google (Development and Testing only)")
                .AllowAnonymous()
                .RequireRateLimiting(RateLimitPolicies.Auth)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status429TooManyRequests);
        }

        api.MapGet("/auth/config", GetConfig)
            .WithName("GetAuthConfig")
            .WithSummary("Public sign-in configuration")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        api.MapPost("/auth/logout", LogoutAsync)
            .WithName("AuthLogout")
            .WithSummary("End the session")
            .AllowWithoutNickname()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        api.MapUsers();
        return api;
    }

    internal static bool IsDevLoginEnabled(IHostEnvironment env) => env.IsDevelopment() || env.IsEnvironment("Testing");

    private static Ok<AuthConfigResponse> GetConfig(IOptions<AuthOptions> options, IHostEnvironment env)
    {
        var clientId = string.IsNullOrWhiteSpace(options.Value.GoogleClientId) ? null : options.Value.GoogleClientId;
        return TypedResults.Ok(new AuthConfigResponse(clientId, IsDevLoginEnabled(env)));
    }

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> GoogleLoginAsync(
        GoogleLoginRequest request,
        IGoogleTokenValidator validator,
        IUserAccounts accounts,
        IOptions<AuthOptions> options,
        HttpContext http,
        CancellationToken ct)
    {
        var identity = await validator.ValidateAsync(request.IdToken, ct);
        if (identity is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The Google ID token is invalid, expired or issued for another client.",
                extensions: new Dictionary<string, object?> { [ProblemCodes.CodeExtension] = ErrorCodes.AuthInvalidToken });
        }

        // Admin rights follow the configured emails, but only for an email Google has verified.
        var isAdmin = identity.EmailVerified && options.Value.IsAdmin(identity.Email);
        var user = await accounts.SignInAsync(new ExternalLogin(identity.Subject, identity.Email, identity.PictureUrl, isAdmin), ct);

        await AppClaims.SignInAsync(http, user);
        return TypedResults.Ok(MeResponse.From(user));
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
}
