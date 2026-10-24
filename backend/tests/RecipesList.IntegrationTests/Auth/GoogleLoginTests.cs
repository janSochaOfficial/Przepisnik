using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Fridges;
using RecipesList.Core.Users;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Auth;

/// <summary><c>POST /auth/google</c> with a fake token validator (API.md §1, TECHNICAL §3.5).</summary>
public sealed class GoogleLoginTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string AdminEmail = "szef@example.com";

    private readonly FakeGoogleTokenValidator _google = new();
    private WebApplicationFactory<Program> _app = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _app = factory.WithFakeGoogle(_google, ("Admin:Emails:0", AdminEmail));
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ValidToken_NewUser_CreatesTheUserWithTheDefaultFridgeAndSignsIn()
    {
        using var client = _app.CreateApiClient();
        var token = _google.Issue("google-sub-1", "ania@example.com", pictureUrl: "https://lh3.example.com/a.png");

        var me = await ContractAssert.IsJsonAsync(
            await client.PostJsonAsync("/api/v1/auth/google", new { idToken = token }), HttpStatusCode.OK, "Me");

        me.GetProperty("email").GetString().ShouldBe("ania@example.com");
        me.GetProperty("avatarUrl").GetString().ShouldBe("https://lh3.example.com/a.png");
        me.GetProperty("nickname").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        me.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
        me.GetProperty("settings").GetProperty("liquidUnits").GetString().ShouldBe("kitchen");
        var userId = me.GetProperty("id").GetGuid();
        var fridgeId = me.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid();

        await using var db = factory.CreateDbContext();
        var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
        user.GoogleSubject.ShouldBe("google-sub-1");
        var fridge = await db.Fridges.Include(f => f.Members).SingleAsync(TestContext.Current.CancellationToken);
        fridge.Id.ShouldBe(fridgeId);
        fridge.Name.ShouldBe(UserDefaults.DefaultFridgeName);
        fridge.OwnerId.ShouldBe(userId);
        fridge.Members.ShouldHaveSingleItem().Role.ShouldBe(FridgeRole.Owner);

        var session = await ContractAssert.IsJsonAsync(await client.GetAsync("/api/v1/me"), HttpStatusCode.OK, "Me");
        session.GetProperty("id").GetGuid().ShouldBe(userId);
    }

    [Fact]
    public async Task SecondLogin_SameSubject_RefreshesEmailAndAvatarWithoutDuplicates()
    {
        using var first = _app.CreateApiClient();
        using var second = _app.CreateApiClient();
        var a = await LoginAsync(first, _google.Issue("google-sub-2", "old@example.com", pictureUrl: "https://lh3.example.com/old.png"));

        var b = await LoginAsync(second, _google.Issue("google-sub-2", "new@example.com", pictureUrl: "https://lh3.example.com/new.png"));

        b.GetProperty("id").GetGuid().ShouldBe(a.GetProperty("id").GetGuid());
        b.GetProperty("email").GetString().ShouldBe("new@example.com");
        b.GetProperty("avatarUrl").GetString().ShouldBe("https://lh3.example.com/new.png");
        b.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid()
            .ShouldBe(a.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid());
        await using var db = factory.CreateDbContext();
        (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await db.Fridges.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentFirstLogins_SameSubject_CreateOneUserAndOneFridge()
    {
        var clients = Enumerable.Range(0, 4).Select(_ => _app.CreateApiClient()).ToList();
        try
        {
            var responses = await Task.WhenAll(clients.Select(c =>
                c.PostJsonAsync("/api/v1/auth/google", new { idToken = _google.Issue("google-sub-race", "race@example.com") })));

            responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
            await using var db = factory.CreateDbContext();
            (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
            (await db.Fridges.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        }
        finally
        {
            clients.ForEach(c => c.Dispose());
        }
    }

    [Fact]
    public async Task InvalidToken_Returns401InvalidTokenAndNoSession()
    {
        using var client = _app.CreateApiClient();

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/auth/google", new { idToken = "not-a-real-token" }),
            HttpStatusCode.Unauthorized,
            "auth.invalid_token");

        (await client.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var db = factory.CreateDbContext();
        (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task MissingIdToken_Returns400()
    {
        using var client = _app.CreateApiClient();

        var response = await client.PostJsonAsync("/api/v1/auth/google", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminEmail_Verified_GetsTheAdminFlag()
    {
        using var client = _app.CreateApiClient();

        var me = await LoginAsync(client, _google.Issue("google-sub-admin", "Szef@Example.com"));

        me.GetProperty("isAdmin").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task AdminEmail_NotVerifiedByGoogle_DoesNotGetTheAdminFlag()
    {
        using var client = _app.CreateApiClient();

        var me = await LoginAsync(client, _google.Issue("google-sub-fake-admin", AdminEmail, emailVerified: false));

        me.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task AdminFlag_RemovedFromConfiguration_IsDroppedOnTheNextLogin()
    {
        using (var client = _app.CreateApiClient())
        {
            (await LoginAsync(client, _google.Issue("google-sub-ex-admin", AdminEmail))).GetProperty("isAdmin").GetBoolean().ShouldBeTrue();
        }

        await using var withoutAdmins = factory.WithFakeGoogle(_google, ("Admin:Emails:0", "someone-else@example.com"));
        using var again = withoutAdmins.CreateApiClient();

        (await LoginAsync(again, _google.Issue("google-sub-ex-admin", AdminEmail))).GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task AuthEndpoints_OverTheLimit_Return429RateLimited()
    {
        await using var limited = factory.WithFakeGoogle(_google, ("RateLimiting:AuthPerMinute", "2"));
        using var client = limited.CreateApiClient();

        (await client.PostJsonAsync("/api/v1/auth/google", new { idToken = "bad" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/auth/config")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/auth/google", new { idToken = "bad" }), HttpStatusCode.TooManyRequests, "rate_limited");
    }

    private static async Task<System.Text.Json.JsonElement> LoginAsync(ApiClient client, string token) =>
        await ContractAssert.IsJsonAsync(
            await client.PostJsonAsync("/api/v1/auth/google", new { idToken = token }), HttpStatusCode.OK, "Me");
}
