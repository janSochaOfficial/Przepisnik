using RecipesList.Core.Common;

namespace RecipesList.Api.Middleware;

/// <summary>Fallback error codes for responses that were not produced by a domain exception.</summary>
internal static class ProblemCodes
{
    public const string CodeExtension = "code";

    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.RequestInvalid,
        StatusCodes.Status401Unauthorized => ErrorCodes.AuthRequired,
        StatusCodes.Status403Forbidden => ErrorCodes.RequestForbidden,
        StatusCodes.Status404NotFound => ErrorCodes.RequestNotFound,
        StatusCodes.Status409Conflict => ErrorCodes.ConcurrencyConflict,
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
        >= 500 => ErrorCodes.ServerError,
        _ => ErrorCodes.RequestInvalid,
    };
}
