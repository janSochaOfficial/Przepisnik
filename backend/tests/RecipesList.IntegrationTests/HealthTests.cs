using System.Net;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests;

public sealed class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/health/ready")]
    public async Task Get_Anonymous_Returns200Ok(string path)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadJsonAsync(response)).GetProperty("status").GetString().ShouldBe("ok");
    }
}
