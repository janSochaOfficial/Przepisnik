using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace RecipesList.IntegrationTests.Infrastructure;

/// <summary>
/// Fails any request whose JSON response carries something shaped like an email address (<c>x@y</c>) outside the
/// caller's own top-level <c>email</c> field (<c>/me</c> and the login responses). A bare <c>@nick</c>, as in the import
/// footer, is fine. Every <see cref="ApiClient"/> goes through it, so the whole integration
/// suite doubles as a scan for leaked email addresses (emails are never shown to other users).
/// </summary>
public sealed partial class EmailLeakGuard : DelegatingHandler
{
    private static readonly string[] OwnEmailPaths =
        ["/api/v1/me", "/api/v1/me/nickname", "/api/v1/me/settings", "/api/v1/auth/dev-login", "/api/v1/auth/google"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return response;
        }

        await response.Content.LoadIntoBufferAsync(cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!text.Contains('@', StringComparison.Ordinal))
        {
            return response;
        }

        var path = request.RequestUri?.AbsolutePath ?? "";
        var allowOwnEmail = OwnEmailPaths.Contains(path, StringComparer.Ordinal);
        using var document = JsonDocument.Parse(text);
        var leak = FindLeak(document.RootElement, "$", allowOwnEmail);
        if (leak is not null)
        {
            throw new InvalidOperationException(
                $"Possible email leak: {request.Method} {path} returned an email-like value at {leak}. Only the caller's "
                + "own email in /me and the login responses may contain one.");
        }

        return response;
    }

    /// <summary>Returns the JSON path of the first string or property name that contains an email-like value, if any.</summary>
    public static string? FindLeak(JsonElement element, string path, bool allowOwnEmail)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = $"{path}.{property.Name}";
                    if (EmailLike().IsMatch(property.Name))
                    {
                        return childPath;
                    }

                    if (allowOwnEmail && path == "$" && property.Name == "email")
                    {
                        continue;
                    }

                    if (FindLeak(property.Value, childPath, allowOwnEmail) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (FindLeak(item, $"{path}[{index++}]", allowOwnEmail) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            case JsonValueKind.String:
                return EmailLike().IsMatch(element.GetString()!) ? path : null;
            default:
                return null;
        }
    }

    [GeneratedRegex(@"[^\s@]+@[^\s@]+")]
    private static partial Regex EmailLike();
}

/// <summary>Creates <see cref="ApiClient"/>s for any factory: https base address, own cookie jar, no redirects, email-leak guard.</summary>
public static class ApiClients
{
    public static ApiClient Create(WebApplicationFactory<Program> factory) =>
        new(factory.CreateDefaultClient(new Uri("https://localhost"), new CookieContainerHandler(), new EmailLeakGuard()));
}
