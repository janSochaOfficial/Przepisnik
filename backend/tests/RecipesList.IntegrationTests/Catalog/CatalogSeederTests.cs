using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Infrastructure.Seeding;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Catalog;

/// <summary>The idempotent startup seeder (TECHNICAL §3.4): insert only when missing, never overwrite.</summary>
public sealed class CatalogSeederTests(ApiFactory factory) : CatalogTestBase(factory)
{
    private const string ProductsJson = """
        { "products": [
          { "name": "Cebula", "category": "vegetables", "measureKind": "count", "displayClass": "piece", "hasSizes": true, "allowHalf": true,
            "gramsPerPiece": 110, "pieceForms": { "one": "sztuka", "few": "sztuki", "many": "sztuk", "fraction": "sztuki" } },
          { "name": "Śmietana 18%", "category": "dairy", "measureKind": "volume", "displayClass": "liquid", "hasSizes": false, "allowHalf": false,
            "defaultPackageAmount": 330 },
          { "name": "  śmietana   18% ", "category": "dairy", "measureKind": "volume", "displayClass": "liquid", "hasSizes": false, "allowHalf": false }
        ] }
        """;

    private static string WriteSeedDirectory(string? products = ProductsJson)
    {
        var dir = Directory.CreateTempSubdirectory("seed-test-").FullName;
        if (products is not null)
        {
            File.WriteAllText(Path.Combine(dir, "products.json"), products);
        }

        File.WriteAllText(Path.Combine(dir, "tags.json"), """{ "tags": [ { "name": "szybkie", "isSystem": true }, { "name": "obiad", "isSystem": false } ] }""");
        File.WriteAllText(Path.Combine(dir, "utensils.json"), """{ "utensils": [ { "name": "Blender" } ] }""");
        return dir;
    }

    private async Task<SeedResult> SeedAsync(string directory)
    {
        await using var db = Factory.CreateDbContext();
        return await new CatalogSeeder(db, TimeProvider.System).SeedAsync(directory, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Seeding_Twice_CreatesNoDuplicates()
    {
        var dir = WriteSeedDirectory();

        (await SeedAsync(dir)).ShouldBe(new SeedResult(2, 2, 1));
        (await SeedAsync(dir)).ShouldBe(new SeedResult(0, 0, 0));

        await using var db = Factory.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        (await db.Products.CountAsync(ct)).ShouldBe(2);
        (await db.Tags.CountAsync(ct)).ShouldBe(2);
        (await db.Utensils.CountAsync(ct)).ShouldBe(1);
        (await db.Tags.SingleAsync(t => t.NormalizedName == "szybkie", ct)).IsSystem.ShouldBeTrue();
        var onion = await db.Products.SingleAsync(p => p.NormalizedName == "cebula", ct);
        onion.PieceForms!.Few.ShouldBe("sztuki");
        onion.GramsPerPiece.ShouldBe(110m);
        onion.CreatedById.ShouldBeNull();
        onion.IsVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task Seeding_DoesNotOverwriteAdminEdits()
    {
        var dir = WriteSeedDirectory();
        await SeedAsync(dir);
        await using (var db = Factory.CreateDbContext())
        {
            var onion = await db.Products.SingleAsync(p => p.NormalizedName == "cebula", TestContext.Current.CancellationToken);
            onion.Category = ProductCategory.Other;
            onion.GramsPerPiece = 99;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync(dir);

        await using var check = Factory.CreateDbContext();
        var stored = await check.Products.SingleAsync(p => p.NormalizedName == "cebula", TestContext.Current.CancellationToken);
        stored.Category.ShouldBe(ProductCategory.Other);
        stored.GramsPerPiece.ShouldBe(99m);
    }

    [Fact]
    public async Task Seeding_MissingOrEmptyFiles_IsFine()
    {
        var dir = WriteSeedDirectory(products: null);
        File.WriteAllText(Path.Combine(dir, "utensils.json"), """{ "utensils": [] }""");

        (await SeedAsync(dir)).ShouldBe(new SeedResult(0, 2, 0));
        (await SeedAsync(Path.Combine(dir, "nothing-here"))).ShouldBe(new SeedResult(0, 0, 0));
    }

    [Fact]
    public async Task Seeding_AnInvalidProduct_FailsLoudly()
    {
        var dir = WriteSeedDirectory("""{ "products": [ { "name": "Woda", "category": "drinks", "measureKind": "mass", "displayClass": "liquid", "hasSizes": false, "allowHalf": false } ] }""");

        await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(dir));
    }

    [Fact]
    public async Task Seeding_TheRealSeedFiles_LoadsCleanly_AndASecondRunInsertsNothing()
    {
        var real = FindRealSeedDirectory();

        var first = await SeedAsync(real);
        var second = await SeedAsync(real);

        second.ShouldBe(new SeedResult(0, 0, 0));
        await using var db = Factory.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        (await db.Products.CountAsync(ct)).ShouldBe(first.Products);
        (await db.Tags.CountAsync(ct)).ShouldBe(first.Tags);
        (await db.Utensils.CountAsync(ct)).ShouldBe(first.Utensils);
        (await db.Tags.CountAsync(t => t.IsSystem, ct)).ShouldBeLessThanOrEqualTo(1);

        // The reserved name stays reserved even though the seed created the system tag.
        using var client = await LoginAsync("ania");
        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/tags", new { name = "szybkie" }), System.Net.HttpStatusCode.BadRequest, "tag.reserved");
    }

    private static string FindRealSeedDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "shared", "seed");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("shared/seed not found above the test binaries.");
    }
}
