using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;

namespace RecipesList.Api.Middleware;

/// <summary>
/// Maps domain exceptions to RFC 9457 ProblemDetails with the <c>code</c> extension (TECHNICAL §3.6).
/// Unknown exceptions fall through to the default handler (500 <c>server.error</c>).
/// </summary>
internal sealed partial class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            DomainValidationException e => Validation(e),
            NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not Found", e),
            ForbiddenException e => Problem(StatusCodes.Status403Forbidden, "Forbidden", e),
            ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflict", e),
            UnauthenticatedException e => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", e),
            DbUpdateConcurrencyException => Coded(StatusCodes.Status409Conflict, "Conflict", ErrorCodes.ConcurrencyConflict),
            // Malformed JSON, missing body, wrong content type, too-large body.
            BadHttpRequestException e => Coded(e.StatusCode, "Bad Request", ErrorCodes.RequestInvalid),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        var status = problem.Status ?? StatusCodes.Status500InternalServerError;
        var code = problem.Extensions[ProblemCodes.CodeExtension] as string;
        var exceptionType = exception.GetType().Name;
        LogHandled(logger, status, code, exceptionType);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, DomainException e)
    {
        var problem = Coded(status, title, e.Code);
        foreach (var (key, value) in e.Extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }

    private static HttpValidationProblemDetails Validation(DomainValidationException e)
    {
        var problem = new HttpValidationProblemDetails(e.Errors.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
        };
        problem.Extensions[ProblemCodes.CodeExtension] = e.Code;
        foreach (var (key, value) in e.Extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }

    private static ProblemDetails Coded(int status, string title, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions[ProblemCodes.CodeExtension] = code;
        return problem;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {ExceptionType} as {Status} {Code}")]
    private static partial void LogHandled(ILogger logger, int status, string? code, string exceptionType);
}
