using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RecipesList.Core.Images;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;
using RecipesList.Infrastructure.Persistence;
using RecipesList.Infrastructure.Storage;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Images;

/// <summary>Orphan cleanup (TECHNICAL §3.7): unreferenced images older than 24 hours go, everything else stays.</summary>
public sealed class OrphanImageCleanerTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private User _owner = null!;
    private string _root = "";
    private LocalDiskImageStorage _storage = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _owner = TestData.User("owner");
        await using var db = factory.CreateDbContext();
        db.Users.Add(_owner);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _root = Path.Combine(Path.GetTempPath(), "recipes-list-tests", Guid.NewGuid().ToString("N"));
        _storage = new LocalDiskImageStorage(new StorageOptions { ImagesPath = _root });
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Run_UnreferencedImageOlderThan24Hours_IsDeletedWithItsFiles()
    {
        var old = await AddImageAsync(age: TimeSpan.FromHours(25));

        var removed = await RunAsync();

        removed.ShouldBe(1);
        await using var db = factory.CreateDbContext();
        (await db.Images.AnyAsync(i => i.Id == old, TestContext.Current.CancellationToken)).ShouldBeFalse();
        _storage.Open(old, ImageSize.Full).ShouldBeNull();
        _storage.Open(old, ImageSize.Thumb).ShouldBeNull();
    }

    [Fact]
    public async Task Run_UnreferencedImageYoungerThan24Hours_IsKept()
    {
        var fresh = await AddImageAsync(age: TimeSpan.FromHours(23));

        (await RunAsync()).ShouldBe(0);

        await using var db = factory.CreateDbContext();
        (await db.Images.AnyAsync(i => i.Id == fresh, TestContext.Current.CancellationToken)).ShouldBeTrue();
        using var file = _storage.Open(fresh, ImageSize.Full);
        file.ShouldNotBeNull();
    }

    [Fact]
    public async Task Run_TimeAdvances_AnImageBecomesDeletable()
    {
        var image = await AddImageAsync(age: TimeSpan.FromHours(23));
        (await RunAsync()).ShouldBe(0);

        _time.Advance(TimeSpan.FromHours(2));

        (await RunAsync()).ShouldBe(1);
        await using var db = factory.CreateDbContext();
        (await db.Images.AnyAsync(i => i.Id == image, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_ImageUsedAsCover_IsKept()
    {
        var cover = await AddImageAsync(TimeSpan.FromDays(3));
        await AddRecipeAsync(r => r.CoverImageId = cover);

        (await RunAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Run_ImageInARecipeStepBlock_IsKept()
    {
        var inStep = await AddImageAsync(TimeSpan.FromDays(3));
        var recipe = await AddRecipeAsync();
        await using (var db = factory.CreateDbContext())
        {
            db.RecipeSteps.Add(new RecipeStep
            {
                RecipeId = recipe,
                Section = StepSection.Instructions,
                Blocks = [new TextBlock("Mieszaj."), new ImageBlock(inStep, "Tak ma wyglądać")],
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await RunAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Run_ImageInAVariantStepOp_IsKept()
    {
        var inOp = await AddImageAsync(TimeSpan.FromDays(3));
        var recipe = await AddRecipeAsync();
        await using (var db = factory.CreateDbContext())
        {
            var variant = TestData.Variant(new Recipe { Id = recipe, AuthorId = _owner.Id, Title = "x", NormalizedTitle = "x" }, _owner, VisibilityLevel.Private);
            variant.StepOps =
            [
                new AddStepOp(StepSection.Instructions, Guid.NewGuid(), null, [new TextBlock("Dodaj"), new ImageBlock(inOp, null)]),
            ];
            db.Variants.Add(variant);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await RunAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Run_MixOfImages_DeletesExactlyTheUnreferencedOldOnes()
    {
        var cover = await AddImageAsync(TimeSpan.FromDays(3));
        var orphanA = await AddImageAsync(TimeSpan.FromDays(2));
        var orphanB = await AddImageAsync(TimeSpan.FromHours(30));
        var fresh = await AddImageAsync(TimeSpan.FromHours(1));
        await AddRecipeAsync(r => r.CoverImageId = cover);

        (await RunAsync()).ShouldBe(2);

        await using var db = factory.CreateDbContext();
        var left = await db.Images.Select(i => i.Id).ToListAsync(TestContext.Current.CancellationToken);
        left.ShouldBe([cover, fresh], ignoreOrder: true);
        left.ShouldNotContain(orphanA);
        left.ShouldNotContain(orphanB);
    }

    private async Task<int> RunAsync()
    {
        await using var db = factory.CreateDbContext();
        return await new OrphanImageCleaner(db, _storage, _time, NullLogger<OrphanImageCleaner>.Instance)
            .RunAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> AddImageAsync(TimeSpan age)
    {
        await using var upload = new MemoryStream(ImageFixtures.Png(32, 32));
        var stored = await _storage.SaveAsync(upload, TestContext.Current.CancellationToken);

        await using var db = factory.CreateDbContext();
        db.Images.Add(new Image
        {
            Id = stored.Id,
            OwnerId = _owner.Id,
            Width = stored.Width,
            Height = stored.Height,
            SizeBytes = stored.SizeBytes,
            CreatedAt = _time.GetUtcNow() - age,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return stored.Id;
    }

    private async Task<Guid> AddRecipeAsync(Action<Recipe>? configure = null)
    {
        var recipe = TestData.Recipe(_owner, VisibilityLevel.Private);
        configure?.Invoke(recipe);
        await using var db = factory.CreateDbContext();
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return recipe.Id;
    }
}
