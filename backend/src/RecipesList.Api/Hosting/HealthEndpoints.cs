using Microsoft.AspNetCore.Http.HttpResults;
using RecipesList.Infrastructure.Persistence;

namespace RecipesList.Api.Hosting;

/// <summary>Liveness and readiness probes (TECHNICAL §9), outside <c>/api/v1</c> and without auth.</summary>
internal static class HealthEndpoints
{
    /// <summary>Health status body.</summary>
    public sealed record HealthResponse(string Status);

    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => TypedResults.Ok(new HealthResponse("ok")))
            .WithName("Health")
            .WithSummary("Liveness probe")
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/api/health/ready", ReadyAsync)
            .WithName("HealthReady")
            .WithSummary("Readiness probe (database reachable)")
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> ReadyAsync(AppDbContext db, CancellationToken ct)
    {
        var ok = await db.Database.CanConnectAsync(ct);
        return ok
            ? TypedResults.Ok(new HealthResponse("ok"))
            : TypedResults.Json(new HealthResponse("unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
