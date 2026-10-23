namespace RecipesList.Core.Common;

/// <summary>The authenticated caller. Every service method that needs authorization takes it (TECHNICAL §3.5).</summary>
public interface ICurrentUser
{
    /// <summary>The user id. Throws <see cref="UnauthenticatedException"/> when nobody is signed in.</summary>
    Guid Id { get; }

    /// <summary>Whether the user is an admin (granted through <c>Admin:Emails</c>).</summary>
    bool IsAdmin { get; }

    /// <summary>Whether a user is signed in at all.</summary>
    bool IsAuthenticated { get; }
}
