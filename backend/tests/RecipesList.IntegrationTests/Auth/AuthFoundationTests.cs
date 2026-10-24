using System.Net;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Fridges;
using RecipesList.Core.Users;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Auth;

/// <summary>The cross-cutting pipeline from T00: dev-login, cookie auth, 401/400/403/404/501 ProblemDetails.</summary>
public sealed class AuthFoundationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task DevLogin_NewUser_ReturnsMeAndCreatesTheDefaultFridge()
    {
        using var client = factory.CreateApiClient();

        var me = await client.LoginAsync("ania");

        ContractAssert.MatchesSchema(me, "Me");
        me.GetProperty("nickname").GetString().ShouldBe("ania");
        me.GetProperty("email").GetString().ShouldBe("ania@test.local");
        var fridgeId = me.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid();

        await using var db = factory.CreateDbContext();
        var fridge = await db.Fridges.Include(f => f.Members).SingleAsync(f => f.Id == fridgeId, TestContext.Current.CancellationToken);
        fridge.Name.ShouldBe(UserDefaults.DefaultFridgeName);
        fridge.OwnerId.ShouldBe(client.UserId);
        fridge.Members.ShouldHaveSingleItem().Role.ShouldBe(FridgeRole.Owner);
    }

    [Fact]
    public async Task DevLogin_SameEmailTwice_ReturnsTheSameUserWithoutDuplicates()
    {
        using var first = factory.CreateApiClient();
        using var second = factory.CreateApiClient();

        var a = await first.LoginAsync("bartek");
        var b = await second.LoginAsync("bartek");

        b.GetProperty("id").GetGuid().ShouldBe(a.GetProperty("id").GetGuid());
        await using var db = factory.CreateDbContext();
        (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await db.Fridges.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task GetMe_AfterDevLogin_ReturnsTheSessionUser()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("celina");

        var me = await ContractAssert.IsJsonAsync(await client.GetAsync("/api/v1/me"), HttpStatusCode.OK, "Me");

        me.GetProperty("id").GetGuid().ShouldBe(client.UserId);
    }

    [Fact]
    public async Task GetMe_NotLoggedIn_Returns401AuthRequired()
    {
        using var client = factory.CreateApiClient();

        await ContractAssert.IsProblemAsync(await client.GetAsync("/api/v1/me"), HttpStatusCode.Unauthorized, "auth.required");
    }

    [Fact]
    public async Task Post_WithoutCsrfHeader_Returns400MissingCsrfHeader()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostJsonAsync("/api/v1/auth/dev-login", new { email = "dawid@test.local" });

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "request.missing_csrf_header");
    }

    [Fact]
    public async Task Logout_SignedIn_EndsTheSession()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("ewa");

        (await client.PostJsonAsync("/api/v1/auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GatedEndpoint_WithoutNickname_Returns403OnboardingRequired()
    {
        using var client = factory.CreateApiClient();
        var me = await client.LoginAsync("filip", onboarded: false);
        me.GetProperty("nickname").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

        await ContractAssert.IsProblemAsync(
            await client.GetAsync("/api/v1/favorites"), HttpStatusCode.Forbidden, "user.onboarding_required");
        (await client.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DevLogin_TakenNickname_Returns409NicknameTaken()
    {
        using var owner = factory.CreateApiClient();
        await owner.LoginAsync("Gosia");
        using var client = factory.CreateApiClient();
        client.EnableCsrfHeader();

        var response = await client.PostJsonAsync("/api/v1/auth/dev-login", new { email = "other@test.local", nickname = "gosia" });

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Conflict, "nickname.taken");
    }

    [Fact]
    public async Task UnknownRoute_Returns404RequestNotFound()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("irena");

        await ContractAssert.IsProblemAsync(await client.GetAsync("/api/v1/nope"), HttpStatusCode.NotFound, "request.not_found");
    }

    [Fact]
    public async Task AuthConfig_Anonymous_EnablesDevLoginInTesting()
    {
        using var client = factory.CreateApiClient();

        var config = await ContractAssert.IsJsonAsync(await client.GetAsync("/api/v1/auth/config"), HttpStatusCode.OK, "AuthConfig");

        config.GetProperty("devLoginEnabled").GetBoolean().ShouldBeTrue();
    }
}
