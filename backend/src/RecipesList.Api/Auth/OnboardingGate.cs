using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;

namespace RecipesList.Api.Auth;

/// <summary>Marks endpoints that work before the user has picked a nickname (<c>/auth/*</c>, <c>/me*</c>, …).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class AllowWithoutNicknameAttribute : Attribute;

/// <summary>
/// Returns 403 <c>user.onboarding_required</c> while the signed-in user has no nickname (API.md §1).
/// Applied to the whole <c>/api/v1</c> group; endpoints opt out with <see cref="OnboardingGateExtensions.AllowWithoutNickname{TBuilder}"/>.
/// </summary>
internal sealed class OnboardingGateFilter(IAppDbContext db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var metadata = http.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<AllowWithoutNicknameAttribute>() is not null
            || metadata?.GetMetadata<IAllowAnonymous>() is not null
            || http.User.Identity?.IsAuthenticated != true
            || http.User.FindFirst(AppClaims.Onboarded)?.Value == "true")
        {
            return await next(context);
        }

        // The cookie may predate onboarding; the database is the source of truth.
        var userId = Guid.TryParse(http.User.FindFirst(AppClaims.UserId)?.Value, out var id) ? id : Guid.Empty;
        var onboarded = await db.Users
            .Where(u => u.Id == userId)
            .AnyAsync(u => u.Nickname != null, http.RequestAborted);

        return onboarded
            ? await next(context)
            : throw new ForbiddenException(ErrorCodes.UserOnboardingRequired, "Pick a nickname first.");
    }
}

internal static class OnboardingGateExtensions
{
    /// <summary>Lets the endpoint run for signed-in users who have not picked a nickname yet.</summary>
    public static TBuilder AllowWithoutNickname<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AllowWithoutNicknameAttribute());
}
