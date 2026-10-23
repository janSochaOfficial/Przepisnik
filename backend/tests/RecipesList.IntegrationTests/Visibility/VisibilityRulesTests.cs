using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RecipesList.Core.Fridges;
using RecipesList.Core.Users;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Visibility;

/// <summary>
/// PROJECT §4.4 and §6.1 for every recipe × variant visibility combination. The people:
/// <c>author</c> wrote the recipe, <c>variantAuthor</c> wrote the variant, <c>mate</c> shares one fridge with each of
/// them (two different fridges), and <c>stranger</c> shares nothing. The two authors share no fridge.
/// </summary>
public sealed class VisibilityRulesTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private User _author = null!;
    private User _variantAuthor = null!;
    private User _mate = null!;
    private User _stranger = null!;
    private Fridge _authorFridge = null!;

    public static TheoryData<VisibilityLevel, VisibilityLevel> Combinations()
    {
        var data = new TheoryData<VisibilityLevel, VisibilityLevel>();
        foreach (var recipe in Enum.GetValues<VisibilityLevel>())
        {
            foreach (var variant in Enum.GetValues<VisibilityLevel>())
            {
                data.Add(recipe, variant);
            }
        }

        return data;
    }

    public async ValueTask InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _author = TestData.User("author");
        _variantAuthor = TestData.User("variant_author");
        _mate = TestData.User("mate");
        _stranger = TestData.User("stranger");
        _authorFridge = TestData.Fridge("author fridge", _author, _mate);

        await using var db = factory.CreateDbContext();
        db.Users.AddRange(_author, _variantAuthor, _mate, _stranger);
        db.Fridges.AddRange(
            _authorFridge,
            TestData.Fridge("variant author fridge", _variantAuthor, _mate),
            TestData.Fridge("stranger fridge", _stranger));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [MemberData(nameof(Combinations))]
    public async Task VisibleRecipesAndVariants_Combination_MatchTheRules(VisibilityLevel recipeVisibility, VisibilityLevel variantVisibility)
    {
        var (recipeId, variantId) = await ArrangeAsync(recipeVisibility, variantVisibility);

        var recipeVisibleToMate = recipeVisibility != VisibilityLevel.Private;
        var recipePublic = recipeVisibility == VisibilityLevel.Public;
        var expected = new (User Viewer, bool Recipe, bool Variant)[]
        {
            // The recipe author shares no fridge with the variant author, so only a public variant reaches them.
            (_author, true, variantVisibility == VisibilityLevel.Public),
            // The variant author always sees their own variant, even when the recipe is hidden from them.
            (_variantAuthor, recipePublic, true),
            (_mate, recipeVisibleToMate, recipeVisibleToMate && variantVisibility != VisibilityLevel.Private),
            (_stranger, recipePublic, recipePublic && variantVisibility == VisibilityLevel.Public),
        };

        await using var db = factory.CreateDbContext();
        var rules = new VisibilityRules(db);
        foreach (var (viewer, recipe, variant) in expected)
        {
            var ct = TestContext.Current.CancellationToken;
            (await rules.VisibleRecipes(viewer.Id).AnyAsync(r => r.Id == recipeId, ct))
                .ShouldBe(recipe, $"recipe {recipeVisibility} for {viewer.Nickname}");
            (await rules.VisibleVariants(viewer.Id).AnyAsync(v => v.Id == variantId, ct))
                .ShouldBe(variant, $"variant {variantVisibility} on a {recipeVisibility} recipe for {viewer.Nickname}");
        }
    }

    [Fact]
    public async Task VisibleRecipes_FridgesRecipe_IsComputedLiveFromMembership()
    {
        var (recipeId, _) = await ArrangeAsync(VisibilityLevel.Fridges, VisibilityLevel.Private);
        await using var db = factory.CreateDbContext();
        var rules = new VisibilityRules(db);
        var ct = TestContext.Current.CancellationToken;
        (await rules.VisibleRecipes(_mate.Id).AnyAsync(r => r.Id == recipeId, ct)).ShouldBeTrue();

        await db.FridgeMembers.Where(m => m.FridgeId == _authorFridge.Id && m.UserId == _mate.Id).ExecuteDeleteAsync(ct);

        (await rules.VisibleRecipes(_mate.Id).AnyAsync(r => r.Id == recipeId, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task VisibleRecipes_ArchivedRecipe_IsReachableOnlyByItsAuthorAndVariantAuthors()
    {
        var (recipeId, _) = await ArrangeAsync(VisibilityLevel.Public, VisibilityLevel.Public, archived: true);
        await using var db = factory.CreateDbContext();
        var rules = new VisibilityRules(db);
        var ct = TestContext.Current.CancellationToken;

        (await rules.VisibleRecipes(_author.Id).AnyAsync(r => r.Id == recipeId, ct)).ShouldBeTrue();
        (await rules.VisibleRecipes(_variantAuthor.Id).AnyAsync(r => r.Id == recipeId, ct)).ShouldBeTrue();
        (await rules.VisibleRecipes(_stranger.Id).AnyAsync(r => r.Id == recipeId, ct)).ShouldBeFalse();
        (await rules.VisibleRecipes(_mate.Id).AnyAsync(r => r.Id == recipeId, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task VisibleVariants_Query_RunsAsOneSqlStatementWithExists()
    {
        await ArrangeAsync(VisibilityLevel.Fridges, VisibilityLevel.Fridges);
        var counter = new CommandCounter();
        await using var db = factory.CreateDbContext(counter);
        var rules = new VisibilityRules(db);

        var sql = rules.VisibleVariants(_mate.Id).ToQueryString();
        var variants = await rules.VisibleVariants(_mate.Id).ToListAsync(TestContext.Current.CancellationToken);

        variants.ShouldHaveSingleItem();
        counter.Count.ShouldBe(1);
        sql.ShouldContain("EXISTS");
    }

    [Fact]
    public void Model_FridgeMembers_HasTheUserFridgeIndexUsedByVisibility()
    {
        using var db = factory.CreateDbContext();
        var index = db.Model.FindEntityType(typeof(FridgeMember))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(FridgeMember.UserId), nameof(FridgeMember.FridgeId)]));

        index.GetDatabaseName().ShouldBe("ix_fridge_members_user_id_fridge_id");
    }

    private async Task<(Guid RecipeId, Guid VariantId)> ArrangeAsync(
        VisibilityLevel recipeVisibility, VisibilityLevel variantVisibility, bool archived = false)
    {
        var recipe = TestData.Recipe(_author, recipeVisibility);
        recipe.IsArchived = archived;
        var variant = TestData.Variant(recipe, _variantAuthor, variantVisibility);

        await using var db = factory.CreateDbContext();
        db.Recipes.Add(recipe);
        db.Variants.Add(variant);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (recipe.Id, variant.Id);
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

