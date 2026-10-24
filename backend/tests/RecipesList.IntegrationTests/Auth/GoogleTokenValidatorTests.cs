using Microsoft.Extensions.Options;
using RecipesList.Infrastructure.Auth;

namespace RecipesList.IntegrationTests.Auth;

/// <summary>The real validator's offline paths (anything reaching Google's certificates is covered by the fake).</summary>
public sealed class GoogleTokenValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_GoogleSignInNotConfigured_ReturnsNull(string? clientId)
    {
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions { ClientId = clientId }));

        (await validator.ValidateAsync("a.b.c", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("still.not-a-jwt")]
    public async Task ValidateAsync_MalformedToken_ReturnsNull(string token)
    {
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions { ClientId = "client.apps.googleusercontent.com" }));

        (await validator.ValidateAsync(token, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
