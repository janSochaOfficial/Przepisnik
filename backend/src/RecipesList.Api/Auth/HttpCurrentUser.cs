using RecipesList.Core.Common;

namespace RecipesList.Api.Auth;

/// <summary><see cref="ICurrentUser"/> read from the session cookie's claims.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && TryGetId(out _);

    public Guid Id => TryGetId(out var id) ? id : throw new UnauthenticatedException("No user is signed in.");

    public bool IsAdmin => accessor.HttpContext?.User.FindFirst(AppClaims.Admin)?.Value == "true";

    private bool TryGetId(out Guid id)
    {
        id = Guid.Empty;
        var value = accessor.HttpContext?.User.FindFirst(AppClaims.UserId)?.Value;
        return value is not null && Guid.TryParse(value, out id);
    }
}
