using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RecipesList.IntegrationTests.Infrastructure;

/// <summary>
/// An HTTP client for the API with its own session. <see cref="LoginAsync"/> signs in through dev-login and from then
/// on sends the CSRF header (<c>X-Requested-With: fetch</c>) on every request.
/// </summary>
public sealed class ApiClient(HttpClient http) : IDisposable
{
    public const string CsrfHeader = "X-Requested-With";
    public const string CsrfValue = "fetch";

    /// <summary>The underlying client (base address <c>https://localhost</c>, cookies handled).</summary>
    public HttpClient Http { get; } = http;

    /// <summary>The signed-in user's <c>Me</c> body, after <see cref="LoginAsync"/>.</summary>
    public JsonElement Me { get; private set; }

    /// <summary>The signed-in user's id, after <see cref="LoginAsync"/>.</summary>
    public Guid UserId => Me.GetProperty("id").GetGuid();

    /// <summary>
    /// Signs in as <c>&lt;nickname&gt;@test.local</c> via dev-login. With <paramref name="onboarded"/> false the user has
    /// no nickname yet (onboarding pending). The same nickname always maps to the same user.
    /// </summary>
    public async Task<JsonElement> LoginAsync(string nickname, bool onboarded = true)
    {
        EnableCsrfHeader();
        var response = await Http.PostAsJsonAsync(
            "/api/v1/auth/dev-login",
            new { email = $"{nickname}@test.local", nickname = onboarded ? nickname : null },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Me = await ReadJsonAsync(response);
        return Me;
    }

    /// <summary>Sends <c>X-Requested-With: fetch</c> on every following request.</summary>
    public void EnableCsrfHeader()
    {
        if (!Http.DefaultRequestHeaders.Contains(CsrfHeader))
        {
            Http.DefaultRequestHeaders.Add(CsrfHeader, CsrfValue);
        }
    }

    public Task<HttpResponseMessage> GetAsync(string path) => Http.GetAsync(path, TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> PostJsonAsync(string path, object? body) =>
        Http.PostAsync(path, JsonContent(body), TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> PutJsonAsync(string path, object? body) =>
        Http.PutAsync(path, JsonContent(body), TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(string path) => Http.DeleteAsync(path, TestContext.Current.CancellationToken);

    /// <summary>Parses the response body as JSON.</summary>
    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    public void Dispose() => Http.Dispose();

    private static StringContent JsonContent(object? body) =>
        new(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json");
}
