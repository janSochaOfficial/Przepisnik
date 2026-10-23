using RecipesList.Api.Middleware;
using RecipesList.Core.Common;

namespace RecipesList.Api.Hosting;

/// <summary>Every error response is ProblemDetails with a <c>code</c> (TECHNICAL §3.6).</summary>
internal static class ProblemDetailsSetup
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;
            if (!problem.Extensions.ContainsKey(ProblemCodes.CodeExtension))
            {
                problem.Extensions[ProblemCodes.CodeExtension] = problem is HttpValidationProblemDetails
                    ? ErrorCodes.ValidationFailed
                    : ProblemCodes.ForStatus(status);
            }

            problem.Extensions.Remove("exception");
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }
}
