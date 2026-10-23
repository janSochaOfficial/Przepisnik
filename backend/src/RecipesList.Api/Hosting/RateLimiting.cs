using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using RecipesList.Api.Middleware;
using RecipesList.Core.Common;

namespace RecipesList.Api.Hosting;

/// <summary>Names of the rate-limiter policies (TECHNICAL §3.5). Features attach them with <c>RequireRateLimiting</c>.</summary>
internal static class RateLimitPolicies
{
    /// <summary><c>auth/*</c>: per IP.</summary>
    public const string Auth = "auth";

    /// <summary>Image uploads: per user.</summary>
    public const string Images = "images";

    /// <summary>Recipe import: per user.</summary>
    public const string Import = "import";
}

/// <summary>Permits per minute, configurable under <c>RateLimiting</c> (tests raise them).</summary>
internal sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    public int AuthPerMinute { get; set; } = 10;

    public int ImagesPerMinute { get; set; } = 30;

    public int ImportPerMinute { get; set; } = 10;
}

internal static class RateLimitingSetup
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitingOptions.Section).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var problem = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too Many Requests" };
                problem.Extensions[ProblemCodes.CodeExtension] = ErrorCodes.RateLimited;
                var writer = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await writer.WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
            };

            options.AddPolicy(RateLimitPolicies.Auth, http =>
                FixedWindow($"ip:{http.Connection.RemoteIpAddress}", limits.AuthPerMinute));
            options.AddPolicy(RateLimitPolicies.Images, http => FixedWindow(UserKey(http), limits.ImagesPerMinute));
            options.AddPolicy(RateLimitPolicies.Import, http => FixedWindow(UserKey(http), limits.ImportPerMinute));
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(string key, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    private static string UserKey(HttpContext http) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? $"user:{id}" : $"ip:{http.Connection.RemoteIpAddress}";
}
