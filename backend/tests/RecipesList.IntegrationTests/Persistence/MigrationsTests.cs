using Microsoft.EntityFrameworkCore;
using RecipesList.Infrastructure;
using RecipesList.Infrastructure.Persistence;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Persistence;

/// <summary>
/// Owned by the data-model owner (T00, then the coordinator / T90). Task agents skip this trait
/// (<c>scripts/verify.sh --agent</c>) because they never add migrations (plan §4.2, §4.4).
/// </summary>
[Trait("Category", "Migrations")]
public sealed class MigrationsTests
{
    [Fact]
    public void MigrationsAreInSync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(options, "Host=localhost;Database=unused");
        using var db = new AppDbContext(options.Options);

        db.Database.HasPendingModelChanges().ShouldBeFalse(
            "The model has changes that no migration covers. Feature tasks report them; the coordinator adds the migration.");
    }

    [Fact]
    public async Task Migrations_EmptyDatabase_ApplyCleanly()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await PostgresContainer.CreateDatabaseAsync(ct);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>();
            DependencyInjection.ConfigureDbContext(options, connectionString);
            await using var db = new AppDbContext(options.Options);

            await db.Database.MigrateAsync(ct);

            (await db.Database.GetPendingMigrationsAsync(ct)).ShouldBeEmpty();
            (await db.Database.GetAppliedMigrationsAsync(ct)).ShouldContain(m => m.EndsWith("_0001_Initial", StringComparison.Ordinal));
            (await db.Users.CountAsync(ct)).ShouldBe(0);
        }
        finally
        {
            await PostgresContainer.DropDatabaseAsync(connectionString);
        }
    }
}
