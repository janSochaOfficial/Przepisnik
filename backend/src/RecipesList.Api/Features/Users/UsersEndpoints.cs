using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RecipesList.Api.Auth;
using RecipesList.Core.Users;

namespace RecipesList.Api.Features.Users;

/// <summary>The current user: <c>/me*</c> and <c>/users/nickname-available</c> (API.md §1). All work before onboarding.</summary>
internal static class UsersEndpoints
{
    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }

    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder api)
    {
        api.MapGet("/me", GetMeAsync)
            .WithName("GetMe")
            .WithSummary("The signed-in user")
            .AllowWithoutNickname()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        api.MapPut("/me/nickname", SetNicknameAsync)
            .WithName("SetNickname")
            .WithSummary("Set or change the nickname")
            .AllowWithoutNickname()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/users/nickname-available", IsNicknameAvailableAsync)
            .WithName("IsNicknameAvailable")
            .WithSummary("Check whether a nickname is free")
            .AllowWithoutNickname()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        api.MapPut("/me/settings", UpdateSettingsAsync)
            .WithName("UpdateSettings")
            .WithSummary("Update the per-user settings")
            .AllowWithoutNickname()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/me/product-preferences", GetProductPreferencesAsync)
            .WithName("GetProductPreferences")
            .WithSummary("The user's product swap preferences")
            .AllowWithoutNickname()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return api;
    }

    private static async Task<Ok<MeResponse>> GetMeAsync(IUserService users, CancellationToken ct) =>
        TypedResults.Ok(MeResponse.From(await users.GetMeAsync(ct)));

    private static async Task<Ok<MeResponse>> SetNicknameAsync(
        NicknameRequest request,
        IUserService users,
        HttpContext http,
        CancellationToken ct)
    {
        var user = await users.SetNicknameAsync(request.Nickname, ct);

        // Re-issue the cookie so its onboarding claim lets the user past the gate without a database lookup.
        await AppClaims.SignInAsync(http, user);
        return TypedResults.Ok(MeResponse.From(user));
    }

    private static async Task<Ok<NicknameAvailabilityResponse>> IsNicknameAvailableAsync(
        [FromQuery] string nickname,
        IUserService users,
        CancellationToken ct) =>
        TypedResults.Ok(new NicknameAvailabilityResponse(await users.IsNicknameAvailableAsync(nickname, ct)));

    private static async Task<Ok<MeResponse>> UpdateSettingsAsync(MeSettingsRequest request, IUserService users, CancellationToken ct) =>
        TypedResults.Ok(MeResponse.From(await users.UpdateSettingsAsync(request.LiquidUnits, request.DefaultFridgeId, ct)));

    private static async Task<Ok<ProductPreferenceResponse[]>> GetProductPreferencesAsync(IUserService users, CancellationToken ct) =>
        TypedResults.Ok((await users.GetProductPreferencesAsync(ct)).Select(ProductPreferenceResponse.From).ToArray());
}
