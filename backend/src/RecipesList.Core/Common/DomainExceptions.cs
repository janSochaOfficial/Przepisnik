namespace RecipesList.Core.Common;

/// <summary>
/// Base class for expected failures. Each carries a stable error <see cref="Code"/> (API.md §9) that the API
/// returns in the ProblemDetails <c>code</c> extension. Messages are English and meant for developers.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string code, string? message)
        : base(message ?? code)
    {
        Code = code;
    }

    /// <summary>The stable, dotted error code, e.g. <c>recipe.not_found</c>.</summary>
    public string Code { get; }

    /// <summary>Extra ProblemDetails extensions (e.g. <c>existingId</c>, <c>variantCount</c>).</summary>
    public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Adds a ProblemDetails extension and returns the exception for fluent use.</summary>
    public DomainException With(string key, object? value)
    {
        Extensions[key] = value;
        return this;
    }
}

/// <summary>The resource does not exist, or the caller must not learn that it exists → 404.</summary>
public sealed class NotFoundException(string code, string? message = null) : DomainException(code, message);

/// <summary>The caller is known but not allowed to do this → 403.</summary>
public sealed class ForbiddenException(string code, string? message = null) : DomainException(code, message);

/// <summary>The request conflicts with the current state (duplicates, locks, stale versions) → 409.</summary>
public sealed class ConflictException(string code, string? message = null) : DomainException(code, message);

/// <summary>Nobody is signed in → 401.</summary>
public sealed class UnauthenticatedException(string? message = null) : DomainException(ErrorCodes.AuthRequired, message);

/// <summary>A business-rule validation failure → 400, with optional per-field codes.</summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string code, IReadOnlyDictionary<string, string[]>? errors = null, string? message = null)
        : base(code, message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    /// <summary>camelCase field paths (e.g. <c>ingredients[3].quantity</c>) mapped to error codes.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
