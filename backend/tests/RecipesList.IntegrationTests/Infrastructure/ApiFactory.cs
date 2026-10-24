using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipesList.Infrastructure;
using RecipesList.Infrastructure.Persistence;
using Respawn;

namespace RecipesList.IntegrationTests.Infrastructure;

/// <summary>
/// The API in the <c>Testing</c> environment against its own PostgreSQL database (Testcontainers). The schema is built
/// with <c>EnsureCreated</c> from the current model (plan §4.4); <see cref="ResetDatabaseAsync"/> empties it (Respawn).
/// Use it as a class fixture: <c>IClassFixture&lt;ApiFactory&gt;</c>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private Respawner? _respawner;

    /// <summary>The connection string of this factory's database.</summary>
    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await PostgresContainer.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        await using (var db = CreateDbContext())
        {
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (ConnectionString.Length > 0)
        {
            await PostgresContainer.DropDatabaseAsync(ConnectionString);
        }
    }

    /// <summary>Deletes all rows (call it at the start of each test that needs a clean database).</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    /// <summary>A fresh context on this factory's database, outside the app's DI (for arranging and asserting data).</summary>
    public AppDbContext CreateDbContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(builder, ConnectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new AppDbContext(builder.Options);
    }

    /// <summary>A client with its own cookie jar; the session cookie is <c>Secure</c>, so the base address is https.</summary>
    public ApiClient CreateApiClient() =>
        ApiClients.Create(this);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("RateLimiting:AuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:ImagesPerMinute", "100000");
        builder.UseSetting("RateLimiting:ImportPerMinute", "100000");
    }
}
