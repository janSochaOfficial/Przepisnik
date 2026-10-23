using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Persistence;

/// <summary>
/// The jsonb columns keep the API shape and survive PostgreSQL's key reordering (the polymorphic discriminator is not
/// always the first key after a round trip).
/// </summary>
public sealed class JsonbMappingTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task StepsAndVariantOps_RoundTrip_KeepTheirPolymorphicShape()
    {
        var ct = TestContext.Current.CancellationToken;
        var author = TestData.User("jsonb");
        var product = new Product
        {
            Name = "cream",
            NormalizedName = "cream",
            Category = ProductCategory.Dairy,
            MeasureKind = MeasureKind.Volume,
            DisplayClass = DisplayClass.Liquid,
            PieceForms = new PluralForms("a", "b", "c", "d"),
            CreatedAt = TestData.Now,
        };
        var recipe = TestData.Recipe(author, VisibilityLevel.Private);
        var imageId = Guid.CreateVersion7();
        var step = new RecipeStep
        {
            RecipeId = recipe.Id,
            Section = StepSection.Instructions,
            Blocks = [new TextBlock("line 1\nline 2"), new ImageBlock(imageId, "caption")],
        };
        recipe.Steps.Add(step);
        var ingredient = new RecipeIngredient { RecipeId = recipe.Id, ProductId = product.Id, Quantity = new Quantity(200m, Unit.Ml) };
        recipe.Ingredients.Add(ingredient);
        var variant = TestData.Variant(recipe, author, VisibilityLevel.Private);
        variant.IngredientOps =
        [
            new ModifyIngredientOp(ingredient.Id, product.Id, new Quantity(1.5m, Unit.Glass), 375m, null, false),
            new RemoveIngredientOp(Guid.CreateVersion7()),
        ];
        variant.StepOps = [new ReplaceStepOp(StepSection.Instructions, step.Id, [new TextBlock("new")])];

        await using (var db = factory.CreateDbContext())
        {
            db.Users.Add(author);
            db.Products.Add(product);
            db.Recipes.Add(recipe);
            db.Variants.Add(variant);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = factory.CreateDbContext())
        {
            var loaded = await db.RecipeSteps.AsNoTracking().SingleAsync(s => s.Id == step.Id, ct);
            loaded.Blocks.ShouldBe([new TextBlock("line 1\nline 2"), new ImageBlock(imageId, "caption")]);

            var loadedVariant = await db.Variants.AsNoTracking().SingleAsync(v => v.Id == variant.Id, ct);
            loadedVariant.IngredientOps[0].ShouldBe(variant.IngredientOps[0]);
            loadedVariant.IngredientOps[1].ShouldBeOfType<RemoveIngredientOp>();
            loadedVariant.StepOps.ShouldHaveSingleItem().ShouldBeOfType<ReplaceStepOp>().TargetId.ShouldBe(step.Id);

            (await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id, ct)).PieceForms
                .ShouldBe(new PluralForms("a", "b", "c", "d"));
            (await db.RecipeIngredients.AsNoTracking().SingleAsync(i => i.Id == ingredient.Id, ct)).Quantity
                .ShouldBe(new Quantity(200m, Unit.Ml));

            // API field names are what raw SQL sees (e.g. B2 re-pointing productId in variant ops).
            var ops = await db.Database
                .SqlQuery<string>($"SELECT ingredient_ops::text AS \"Value\" FROM variants WHERE id = {variant.Id}")
                .SingleAsync(ct);
            ops.ShouldContain("\"productId\"");
            ops.ShouldContain("\"op\": \"modify\"");
        }
    }
}
