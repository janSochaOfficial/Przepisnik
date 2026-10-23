using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Variants;

/// <summary>The matrix query budget: a recipe with 50 variants is read in at most 3 SQL statements.</summary>
public sealed class VariantMatrixQueryTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const int VariantCount = 50;
    private readonly VariantScenario _s = new();

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetMatrix_FiftyVariants_RunsInAtMostThreeStatements()
    {
        var ct = TestContext.Current.CancellationToken;
        var author = TestData.User("autorka");
        var others = Enumerable.Range(0, 5).Select(i => TestData.User($"kucharz{i}")).ToList();
        Guid recipeId;
        await using (var db = factory.CreateDbContext())
        {
            db.Users.Add(author);
            db.Users.AddRange(others);
            await db.SaveChangesAsync(ct);
            await _s.SeedCatalogAsync(db);
            var recipe = await _s.AddRecipeAsync(db, author.Id, VisibilityLevel.Public);
            recipeId = recipe.Id;
            for (var i = 0; i < VariantCount; i++)
            {
                var variant = TestData.Variant(recipe, others[i % others.Count], VisibilityLevel.Public, $"Wariant {i}");
                variant.CreatedAt = TestData.Now.AddMinutes(i);
                variant.IngredientOps =
                [
                    new ModifyIngredientOp(recipe.Ingredients[1].Id, i % 2 == 0 ? _s.HeavyCream.Id : _s.Cream.Id, new Quantity(200 + i, Unit.Ml), 200 + i, null, false),
                    new AddIngredientOp(Guid.CreateVersion7(), recipe.Ingredients[0].Id, _s.Pepper.Id, new Quantity(1 + (i % 3), Unit.Piece), 1 + (i % 3), null, false),
                ];
                db.Variants.Add(variant);
            }

            await db.SaveChangesAsync(ct);
        }

        var counter = new CommandCounter();
        await using var context = factory.CreateDbContext(counter);
        var user = Substitute.For<ICurrentUser>();
        user.Id.Returns(author.Id);
        var engine = new VariantEngine();
        var visibility = new VisibilityRules(context);
        var service = new VariantService(
            context, user, visibility, engine, new QuantityConverter(), new SelectionMemory(context, user, visibility, engine, TimeProvider.System), TimeProvider.System);

        var result = await service.GetMatrixAsync(recipeId, ct);

        counter.Count.ShouldBeLessThanOrEqualTo(3);
        result.Matrix.Variants.Count.ShouldBe(VariantCount + 1);
        result.Columns.Select(c => c.Name).Skip(1).First().ShouldBe("Wariant 0");
        result.Matrix.Slots.Single(s => s.Key == MatrixKeys.AddedSlot(_s.Pepper.Id)).Options.Count.ShouldBe(4);
        result.Products.Keys.ShouldContain(_s.Pepper.Id);
        result.Products.Keys.ShouldContain(_s.HeavyCream.Id);
    }

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
