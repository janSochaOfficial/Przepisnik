using Microsoft.AspNetCore.Http.HttpResults;

namespace RecipesList.Api.Features;

/// <summary>
/// Placeholder endpoints created by T00 so that every route of the contract exists from day one.
/// A feature task replaces each <c>MapStub</c> call with its real handler (same route, same name).
/// </summary>
internal static class StubEndpoints
{
    public static RouteHandlerBuilder MapStub(this IEndpointRouteBuilder api, string method, string pattern, string name, string summary) =>
        api.MapMethods(pattern, [method], NotImplemented)
            .WithName(name)
            .WithSummary(summary)
            .Produces(StatusCodes.Status501NotImplemented);

    private static StatusCodeHttpResult NotImplemented() => TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
}
