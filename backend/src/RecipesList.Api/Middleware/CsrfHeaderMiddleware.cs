using Microsoft.AspNetCore.Mvc;
using RecipesList.Core.Common;

namespace RecipesList.Api.Middleware;

/// <summary>
/// CSRF defence (TECHNICAL §3.5): every mutating <c>/api</c> request must carry <c>X-Requested-With: fetch</c>.
/// Browsers cannot add that header cross-site without a CORS preflight, which the API never allows.
/// </summary>
internal sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "fetch";

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        if (RequiresHeader(context.Request) && !HasHeader(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = $"Mutating requests must send the header '{HeaderName}: {HeaderValue}'.",
            };
            problem.Extensions[ProblemCodes.CodeExtension] = ErrorCodes.RequestMissingCsrfHeader;
            await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
            return;
        }

        await next(context);
    }

    private static bool RequiresHeader(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || HttpMethods.IsTrace(request.Method));

    private static bool HasHeader(HttpRequest request) =>
        request.Headers.TryGetValue(HeaderName, out var values)
        && values.Any(v => string.Equals(v, HeaderValue, StringComparison.OrdinalIgnoreCase));
}
