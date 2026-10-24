using System.Net;
using System.Text.Json;

namespace RecipesList.IntegrationTests.Infrastructure;

public sealed class EmailLeakGuardTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void FindLeak_NestedEmail_ReturnsItsPath()
    {
        using var document = JsonDocument.Parse("""{"members":[{"nickname":"ania"},{"nickname":"bob","email":"bob@x.pl"}]}""");

        EmailLeakGuard.FindLeak(document.RootElement, "$", allowOwnEmail: true).ShouldBe("$.members[1].email");
    }

    [Fact]
    public void FindLeak_BareNicknameMention_IsNotALeak()
    {
        using var document = JsonDocument.Parse("""{"description":"Zaimportowano — oryginalnie: @ania"}""");

        EmailLeakGuard.FindLeak(document.RootElement, "$", allowOwnEmail: false).ShouldBeNull();
    }

    [Fact]
    public void FindLeak_OwnTopLevelEmail_IsAllowedOnlyWhenPermitted()
    {
        using var document = JsonDocument.Parse("""{"id":"1","email":"ania@x.pl"}""");

        EmailLeakGuard.FindLeak(document.RootElement, "$", allowOwnEmail: true).ShouldBeNull();
        EmailLeakGuard.FindLeak(document.RootElement, "$", allowOwnEmail: false).ShouldBe("$.email");
    }

    [Fact]
    public async Task Me_ReturnsOwnEmailThroughTheGuard()
    {
        using var client = factory.CreateApiClient();
        await client.LoginAsync("leakguard");

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadJsonAsync(response)).GetProperty("email").GetString().ShouldBe("leakguard@test.local");
    }
}
