using System.Net;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Fridges;

/// <summary>A user who is not a member gets 404 <c>fridge.not_found</c> on every fridge route (no existence leak).</summary>
public sealed class FridgeAccessTests(ApiFactory factory) : FridgeTestBase(factory)
{
    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "/fridges/{f}" },
        { "PUT", "/fridges/{f}" },
        { "DELETE", "/fridges/{f}" },
        { "POST", "/fridges/{f}/invites" },
        { "GET", "/fridges/{f}/invites" },
        { "DELETE", "/fridges/{f}/invites/{x}" },
        { "DELETE", "/fridges/{f}/members/{x}" },
        { "GET", "/fridges/{f}/items" },
        { "POST", "/fridges/{f}/items" },
        { "PUT", "/fridges/{f}/items/{x}" },
        { "DELETE", "/fridges/{f}/items/{x}" },
        { "POST", "/fridges/{f}/deductions" },
        { "POST", "/fridges/{f}/deductions/{x}/undo" },
        { "GET", "/fridges/{f}/shopping-list" },
        { "POST", "/fridges/{f}/shopping-list/items" },
        { "PATCH", "/fridges/{f}/shopping-list/items/{x}" },
        { "DELETE", "/fridges/{f}/shopping-list/items/{x}" },
        { "POST", "/fridges/{f}/shopping-list/complete" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task FridgeRoute_NonMember_Returns404(string method, string template)
    {
        using var anna = await LoginAsync("anna");
        using var stranger = await LoginAsync("obcy");
        var fridgeId = await DefaultFridgeAsync(anna);
        var item = await AddItemAsync(anna, fridgeId, Milk, 1000m);

        var path = "/api/v1" + template
            .Replace("{f}", fridgeId.ToString(), StringComparison.Ordinal)
            .Replace("{x}", item.GetProperty("id").GetString(), StringComparison.Ordinal);
        var response = await SendAsync(stranger, method, path, BodyFor(template, method));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "fridge.not_found");
        (await ItemsAsync(anna, fridgeId)).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task ListFridges_NonMember_DoesNotSeeTheFridge()
    {
        using var anna = await LoginAsync("anna");
        using var stranger = await LoginAsync("obcy");
        var fridgeId = await DefaultFridgeAsync(anna);

        var list = await IsJsonArrayAsync(await stranger.GetAsync("/api/v1/fridges"), "FridgeSummary");

        list.EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).ShouldNotContain(fridgeId);
    }

    private object? BodyFor(string template, string method) => (template, method) switch
    {
        ("/fridges/{f}", "PUT") => new { name = "Cudza" },
        ("/fridges/{f}/items", "POST") => new { productId = Milk, packageAmount = 1000, remainingPercent = 100, note = (string?)null },
        ("/fridges/{f}/items/{x}", "PUT") => new { productId = Milk, packageAmount = 1000, remainingPercent = 50, note = (string?)null, version = 1 },
        ("/fridges/{f}/deductions", _) => new { recipeId = (Guid?)null, variantId = (Guid?)null, lines = new[] { new { productId = Milk, amount = 100 } } },
        ("/fridges/{f}/shopping-list/items", _) => new { items = new[] { new { productId = (Guid?)Milk, text = (string?)null, amount = (decimal?)null } } },
        ("/fridges/{f}/shopping-list/items/{x}", "PATCH") => new { version = 1, @checked = true, amount = (decimal?)null, text = (string?)null },
        ("/fridges/{f}/shopping-list/complete", _) => new { moveToFridge = true },
        _ => null,
    };

    private static Task<HttpResponseMessage> SendAsync(ApiClient client, string method, string path, object? body) => method switch
    {
        "GET" => client.GetAsync(path),
        "DELETE" => client.DeleteAsync(path),
        "PUT" => client.PutJsonAsync(path, body),
        "PATCH" => client.PatchJsonAsync(path, body!),
        _ => client.PostJsonAsync(path, body),
    };
}
