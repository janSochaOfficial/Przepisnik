using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RecipesList.Core.Fridges;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Auth;

/// <summary>A first login creates the user, the default fridge and the default setting together, or nothing at all.</summary>
public sealed class FirstLoginAtomicityTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FirstLogin_FridgeCreationFails_LeavesNoUserBehindAndTheNextLoginStartsClean()
    {
        var failingFridges = Substitute.For<IFridgeService>();
        failingFridges.CreateOwnedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new InvalidOperationException("fridge storage is down"));
        using (var broken = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => failingFridges))))
        using (var client = ApiClients.Create(broken))
        {
            client.EnableCsrfHeader();
            var response = await client.Http.PostAsJsonAsync(
                "/api/v1/auth/dev-login", new { email = "pechowiec@test.local", nickname = "pechowiec" }, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }

        await using (var db = factory.CreateDbContext())
        {
            (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        }

        using var retry = factory.CreateApiClient();
        var me = await retry.LoginAsync("pechowiec");

        me.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid().ShouldNotBe(Guid.Empty);
        await using var check = factory.CreateDbContext();
        (await check.Fridges.CountAsync(f => f.OwnerId == retry.UserId, TestContext.Current.CancellationToken)).ShouldBe(1);
    }
}
