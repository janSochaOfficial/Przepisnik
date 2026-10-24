using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;
using RecipesList.Core.ShoppingLists;
using RecipesList.IntegrationTests.Infrastructure;
using RecipesList.Infrastructure.Persistence;

namespace RecipesList.IntegrationTests.Fridges;

/// <summary>Every list read runs a constant number of SQL statements, whatever the number of rows.</summary>
public sealed class FridgeQueryCountTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const int Many = 12;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ListReads_FewOrManyRows_RunTheSameSmallNumberOfStatements()
    {
        var few = await MeasureAsync(await ArrangeAsync("mala", 1));
        var many = await MeasureAsync(await ArrangeAsync("duza", Many));

        many.ShouldBe(few);
        few.Fridges.ShouldBe(1);
        few.Detail.ShouldBe(2);
        few.Invites.ShouldBe(2);
        few.Items.ShouldBe(2);
        few.Shopping.ShouldBe(2);
    }

    private async Task<Counts> MeasureAsync((Guid UserId, Guid FridgeId) s)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = Substitute.For<ICurrentUser>();
        user.Id.Returns(s.UserId);

        return new Counts(
            await CountAsync(user, (db, access) => new FridgeService(db, user, access, TimeProvider.System).ListAsync(ct)),
            await CountAsync(user, (db, access) => new FridgeService(db, user, access, TimeProvider.System).GetAsync(s.FridgeId, ct)),
            await CountAsync(user, (db, access) => new FridgeService(db, user, access, TimeProvider.System).ListInvitesAsync(s.FridgeId, ct)),
            await CountAsync(user, (db, access) => new FridgeItemService(db, user, access, TimeProvider.System).ListAsync(s.FridgeId, ct)),
            await CountAsync(user, (db, access) => new ShoppingListService(db, user, access, TimeProvider.System).ListAsync(s.FridgeId, ct)));
    }

    private async Task<int> CountAsync<T>(ICurrentUser user, Func<AppDbContext, FridgeAccess, Task<T>> read)
    {
        var counter = new CommandCounter();
        await using var db = factory.CreateDbContext(counter);
        await read(db, new FridgeAccess(db, user));
        return counter.Count;
    }

    /// <summary>A user who owns a fridge with <paramref name="n"/> extra members, fridges, invites, items and entries.</summary>
    private async Task<(Guid UserId, Guid FridgeId)> ArrangeAsync(string prefix, int n)
    {
        await using var db = factory.CreateDbContext();
        var owner = TestData.User(prefix);
        var others = Enumerable.Range(0, n).Select(i => TestData.User($"{prefix}{i}")).ToArray();
        db.Users.Add(owner);
        db.Users.AddRange(others);
        var fridge = TestData.Fridge($"{prefix} lodówka", owner, others);
        db.Fridges.Add(fridge);
        for (var i = 0; i < n; i++)
        {
            db.Fridges.Add(TestData.Fridge($"{prefix} {i}", others[i], owner));
            var product = new Product
            {
                Name = $"{prefix} produkt {i}",
                NormalizedName = NameNormalizer.Normalize($"{prefix} produkt {i}"),
                MeasureKind = MeasureKind.Mass,
                DisplayClass = DisplayClass.Weight,
                CreatedAt = TestData.Now,
            };
            db.Products.Add(product);
            db.FridgeItems.Add(new FridgeItem
            {
                FridgeId = fridge.Id,
                ProductId = product.Id,
                PackageAmount = 100m,
                RemainingPercent = 50m,
                AddedById = others[i].Id,
                CreatedAt = TestData.Now,
                UpdatedAt = TestData.Now,
            });
            db.ShoppingListItems.Add(new ShoppingListItem { FridgeId = fridge.Id, ProductId = product.Id, AddedById = others[i].Id, CreatedAt = TestData.Now });
            db.ShoppingListItems.Add(new ShoppingListItem { FridgeId = fridge.Id, Text = $"tekst {i}", AddedById = owner.Id, CreatedAt = TestData.Now });
            db.FridgeInvites.Add(new FridgeInvite
            {
                FridgeId = fridge.Id,
                Token = $"{prefix}-token-{i}",
                CreatedById = owner.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (owner.Id, fridge.Id);
    }

    private sealed record Counts(int Fridges, int Detail, int Invites, int Items, int Shopping);

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
