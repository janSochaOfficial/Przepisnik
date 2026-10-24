using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Variants;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Users;

/// <summary>The current-user endpoints: onboarding, nickname, settings, preferences (API.md §1, PROJECT §2).</summary>
public sealed class UserEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ───────────── onboarding ─────────────

    [Fact]
    public async Task WithoutNickname_Recipes_Returns403OnboardingRequired_ButTheMeEndpointsWork()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("nowy", onboarded: false);

        await ContractAssert.IsProblemAsync(await client.GetAsync("/api/v1/recipes"), HttpStatusCode.Forbidden, "user.onboarding_required");
        await ContractAssert.IsJsonAsync(await client.GetAsync("/api/v1/me"), HttpStatusCode.OK, "Me");
        await ContractAssert.IsJsonAsync(
            await client.GetAsync("/api/v1/users/nickname-available?nickname=nowy"), HttpStatusCode.OK, "NicknameAvailability");
        (await client.GetAsync("/api/v1/me/product-preferences")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/settings", new { liquidUnits = "metric", defaultFridgeId = DefaultFridge(client.Me) }),
            HttpStatusCode.OK,
            "Me");
    }

    [Fact]
    public async Task SetNickname_DuringOnboarding_LetsTheUserIntoTheApp()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("onboard", onboarded: false);

        var me = await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/nickname", new { nickname = "  Żaneta_1  " }), HttpStatusCode.OK, "Me");

        me.GetProperty("nickname").GetString().ShouldBe("Żaneta_1");
        (await client.GetAsync("/api/v1/recipes")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var db = factory.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.Id == client.UserId, TestContext.Current.CancellationToken);
        user.NormalizedNickname.ShouldBe("zaneta_1");
    }

    // ───────────── nickname rules ─────────────

    [Theory]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345")]
    [InlineData("ania kowalska")]
    [InlineData("ania!")]
    [InlineData("ania.k")]
    [InlineData("   ")]
    public async Task SetNickname_BreakingTheRules_Returns400NicknameInvalid(string nickname)
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("reguly", onboarded: false);

        var problem = await ContractAssert.IsProblemAsync(
            await client.PutJsonAsync("/api/v1/me/nickname", new { nickname }), HttpStatusCode.BadRequest, "nickname.invalid");

        problem.GetProperty("errors").GetProperty("nickname")[0].GetString().ShouldBe("nickname.invalid");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234")]
    [InlineData("Łucja-Świątek_7")]
    [InlineData("ĄĆĘŁŃÓŚŹŻ")]
    public async Task SetNickname_FollowingTheRules_Succeeds(string nickname)
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("dobry", onboarded: false);

        var me = await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/nickname", new { nickname }), HttpStatusCode.OK, "Me");

        me.GetProperty("nickname").GetString().ShouldBe(nickname);
    }

    [Theory]
    [InlineData("ania")]
    [InlineData("ANIA")]
    [InlineData("Ánia")]
    public async Task SetNickname_TakenIgnoringCaseAndDiacritics_Returns409NicknameTaken(string nickname)
    {
        using var owner = factory.CreateApiClient();
        await owner.LoginAsync("Ania");
        using var client = factory.CreateApiClient();
        await client.LoginAsync("druga", onboarded: false);

        await ContractAssert.IsProblemAsync(
            await client.PutJsonAsync("/api/v1/me/nickname", new { nickname }), HttpStatusCode.Conflict, "nickname.taken");
    }

    [Fact]
    public async Task SetNickname_OwnNicknameInAnotherCase_IsAllowed()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("kasia");

        var me = await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/nickname", new { nickname = "Kasia" }), HttpStatusCode.OK, "Me");

        me.GetProperty("nickname").GetString().ShouldBe("Kasia");
    }

    [Fact]
    public async Task NicknameAvailable_ReportsFreeTakenInvalidAndOwn()
    {
        using var owner = factory.CreateApiClient();
        await owner.LoginAsync("Zajety");
        using var client = factory.CreateApiClient();
        await client.LoginAsync("pytajacy");

        (await AvailableAsync(client, "wolny")).ShouldBeTrue();
        (await AvailableAsync(client, "zajęty")).ShouldBeFalse();
        (await AvailableAsync(client, "ZAJETY")).ShouldBeFalse();
        (await AvailableAsync(client, "x")).ShouldBeFalse();
        (await AvailableAsync(client, "Pytajacy")).ShouldBeTrue();
    }

    [Fact]
    public async Task NicknameAvailable_WithoutTheParameter_Returns400()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("bezparametru");

        (await client.GetAsync("/api/v1/users/nickname-available")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NicknameAvailable_NotSignedIn_Returns401()
    {
        using var client = factory.CreateApiClient();

        await ContractAssert.IsProblemAsync(
            await client.GetAsync("/api/v1/users/nickname-available?nickname=abc"), HttpStatusCode.Unauthorized, "auth.required");
    }

    // ───────────── settings ─────────────

    [Fact]
    public async Task Settings_LiquidUnits_RoundTrip()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("plyny");
        var fridgeId = DefaultFridge(client.Me);

        var updated = await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/settings", new { liquidUnits = "metric", defaultFridgeId = fridgeId }), HttpStatusCode.OK, "Me");
        var reread = await ContractAssert.IsJsonAsync(await client.GetAsync("/api/v1/me"), HttpStatusCode.OK, "Me");

        updated.GetProperty("settings").GetProperty("liquidUnits").GetString().ShouldBe("metric");
        reread.GetProperty("settings").GetProperty("liquidUnits").GetString().ShouldBe("metric");
        reread.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid().ShouldBe(fridgeId);

        var back = await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/settings", new { liquidUnits = "kitchen", defaultFridgeId = fridgeId }), HttpStatusCode.OK, "Me");
        back.GetProperty("settings").GetProperty("liquidUnits").GetString().ShouldBe("kitchen");
    }

    [Fact]
    public async Task Settings_DefaultFridge_CanBeAnyFridgeIBelongTo()
    {
        using var owner = factory.CreateApiClient();
        await owner.LoginAsync("wlasciciel");
        using var client = factory.CreateApiClient();
        await client.LoginAsync("czlonek");
        var shared = await ContractAssert.IsJsonAsync(
            await owner.PostJsonAsync("/api/v1/fridges", new { name = "Wspólna" }), HttpStatusCode.Created, "FridgeSummary");
        var sharedId = shared.GetProperty("id").GetGuid();
        var invite = await ApiClient.ReadJsonAsync(await owner.PostJsonAsync($"/api/v1/fridges/{sharedId}/invites", null));
        (await client.PostJsonAsync($"/api/v1/invites/{invite.GetProperty("token").GetString()}/accept", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var me = await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/settings", new { liquidUnits = "kitchen", defaultFridgeId = sharedId }), HttpStatusCode.OK, "Me");

        me.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid().ShouldBe(sharedId);
    }

    [Fact]
    public async Task Settings_ForeignFridge_Returns404FridgeNotFoundAndKeepsTheSettings()
    {
        using var stranger = factory.CreateApiClient();
        await stranger.LoginAsync("obcy");
        using var client = factory.CreateApiClient();
        await client.LoginAsync("jaja");

        await ContractAssert.IsProblemAsync(
            await client.PutJsonAsync("/api/v1/me/settings", new { liquidUnits = "metric", defaultFridgeId = DefaultFridge(stranger.Me) }),
            HttpStatusCode.NotFound,
            "fridge.not_found");
        await ContractAssert.IsProblemAsync(
            await client.PutJsonAsync("/api/v1/me/settings", new { liquidUnits = "metric", defaultFridgeId = Guid.CreateVersion7() }),
            HttpStatusCode.NotFound,
            "fridge.not_found");

        var me = await ApiClient.ReadJsonAsync(await client.GetAsync("/api/v1/me"));
        me.GetProperty("settings").GetProperty("liquidUnits").GetString().ShouldBe("kitchen");
        me.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid().ShouldBe(DefaultFridge(client.Me));
    }

    [Fact]
    public async Task Settings_NullDefaultFridge_ClearsIt()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("bezdomyslnej");

        var me = await ContractAssert.IsJsonAsync(
            await client.PutJsonAsync("/api/v1/me/settings", new { liquidUnits = "kitchen", defaultFridgeId = (Guid?)null }), HttpStatusCode.OK, "Me");

        me.GetProperty("settings").GetProperty("defaultFridgeId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("""{ "liquidUnits": "imperial", "defaultFridgeId": null }""")]
    [InlineData("""{ "liquidUnits": 1, "defaultFridgeId": null }""")]
    [InlineData("""{ "defaultFridgeId": null }""")]
    public async Task Settings_MalformedBody_Returns400(string body)
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("zlebody");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await client.Http.PutAsync("/api/v1/me/settings", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ───────────── product preferences ─────────────

    [Fact]
    public async Task ProductPreferences_ReturnsOnlyMineStrongestFirst()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("preferencje");
        using var other = factory.CreateApiClient();
        await other.LoginAsync("inny");
        var (butter, oil, margarine) = (NewProduct("Masło"), NewProduct("Olej"), NewProduct("Margaryna"));
        await using (var db = factory.CreateDbContext())
        {
            db.Products.AddRange(butter, oil, margarine);
            db.ProductPreferences.AddRange(
                new ProductPreference { UserId = client.UserId, FromProductId = butter.Id, ToProductId = oil.Id, Weight = 1 },
                new ProductPreference { UserId = client.UserId, FromProductId = butter.Id, ToProductId = margarine.Id, Weight = 3 },
                new ProductPreference { UserId = other.UserId, FromProductId = oil.Id, ToProductId = butter.Id, Weight = 9 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await client.GetAsync("/api/v1/me/product-preferences");
        var body = await ApiClient.ReadJsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = body.EnumerateArray().ToList();
        items.ForEach(i => ContractAssert.MatchesSchema(i, "ProductPreference"));
        items.Select(i => (i.GetProperty("toProductId").GetGuid(), i.GetProperty("weight").GetInt32()))
            .ShouldBe([(margarine.Id, 3), (oil.Id, 1)]);
        items.ShouldAllBe(i => i.GetProperty("fromProductId").GetGuid() == butter.Id);
    }

    [Fact]
    public async Task ProductPreferences_None_ReturnsAnEmptyArray()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("pusto");

        var body = await ApiClient.ReadJsonAsync(await client.GetAsync("/api/v1/me/product-preferences"));

        body.GetArrayLength().ShouldBe(0);
    }

    // ───────────── privacy ─────────────

    [Fact]
    public async Task OtherUsersResponses_NeverContainMyEmail()
    {
        using var owner = factory.CreateApiClient();
        await owner.LoginAsync("prywatna");
        using var member = factory.CreateApiClient();
        await member.LoginAsync("sasiad");
        var fridgeId = DefaultFridge(owner.Me);
        var invite = await ApiClient.ReadJsonAsync(await owner.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null));
        var token = invite.GetProperty("token").GetString();
        (await member.PostJsonAsync($"/api/v1/invites/{token}/accept", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var seenByMember = new[]
        {
            await member.GetAsync($"/api/v1/fridges/{fridgeId}"),
            await member.GetAsync("/api/v1/fridges"),
            await member.GetAsync($"/api/v1/invites/{token}"),
        };
        var seenByOwner = await owner.GetAsync($"/api/v1/fridges/{fridgeId}");

        foreach (var response in seenByMember)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            text.ShouldNotContain("prywatna@test.local", Case.Insensitive);
            text.ShouldNotContain("@");
        }

        var ownerView = await ContractAssert.IsJsonAsync(seenByOwner, HttpStatusCode.OK, "FridgeDetail");
        ownerView.GetProperty("members").GetArrayLength().ShouldBe(2);
        ownerView.ToString().ShouldNotContain("sasiad@test.local", Case.Insensitive);
    }

    private static Guid DefaultFridge(JsonElement me) => me.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid();

    private static async Task<bool> AvailableAsync(ApiClient client, string nickname)
    {
        var body = await ContractAssert.IsJsonAsync(
            await client.GetAsync($"/api/v1/users/nickname-available?nickname={Uri.EscapeDataString(nickname)}"),
            HttpStatusCode.OK,
            "NicknameAvailability");
        return body.GetProperty("available").GetBoolean();
    }

    private static Product NewProduct(string name) => new()
    {
        Name = name,
        NormalizedName = NameNormalizer.Normalize(name),
        Category = ProductCategory.Other,
        MeasureKind = MeasureKind.Mass,
        DisplayClass = DisplayClass.Weight,
        CreatedAt = TestData.Now,
    };
}
