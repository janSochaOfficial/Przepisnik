using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RecipesList.Infrastructure.Auth;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Auth;

/// <summary>Accepts exactly the tokens registered with <see cref="Issue"/>; every other token is invalid.</summary>
public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly ConcurrentDictionary<string, GoogleIdentity> _tokens = new(StringComparer.Ordinal);

    /// <summary>Registers a token that validates to the given identity and returns it.</summary>
    public string Issue(string subject, string email, bool emailVerified = true, string? pictureUrl = null)
    {
        var token = $"fake-token-{Guid.NewGuid():N}";
        _tokens[token] = new GoogleIdentity(subject, email, emailVerified, pictureUrl);
        return token;
    }

    public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct) =>
        Task.FromResult(_tokens.TryGetValue(idToken, out var identity) ? identity : null);
}

/// <summary>Builds an <see cref="ApiFactory"/> variant that uses a <see cref="FakeGoogleTokenValidator"/>.</summary>
public static class GoogleAuthTestHost
{
    public static WebApplicationFactory<Program> WithFakeGoogle(
        this ApiFactory factory,
        FakeGoogleTokenValidator fake,
        params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services => services.AddSingleton<IGoogleTokenValidator>(fake));
        });

    /// <summary>A client with its own cookie jar that already sends the CSRF header.</summary>
    public static ApiClient CreateApiClient(this WebApplicationFactory<Program> factory)
    {
        var client = new ApiClient(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        }));
        client.EnableCsrfHeader();
        return client;
    }
}
