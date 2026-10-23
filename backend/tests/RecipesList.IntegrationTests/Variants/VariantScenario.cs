using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;
using RecipesList.Infrastructure.Persistence;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Variants;

/// <summary>
/// Catalog data and recipes arranged directly in the database (recipes are B3's endpoints), plus the request bodies
/// the variant editor sends.
/// </summary>
internal sealed class VariantScenario
{
    public Product Chicken { get; } = Product("pierś z kurczaka", MeasureKind.Mass, DisplayClass.Weight, gramsPerPiece: 200);

    public Product Cream { get; } = Product("śmietana 18%", MeasureKind.Volume, DisplayClass.Liquid, gramsPerMl: 1m);

    public Product HeavyCream { get; } = Product("śmietanka 30%", MeasureKind.Volume, DisplayClass.Liquid, gramsPerMl: 1m);

    public Product Pepper { get; } = Product("papryka czerwona", MeasureKind.Count, DisplayClass.Piece, gramsPerPiece: 150, hasSizes: true);

    public Product Salt { get; } = Product("sól", MeasureKind.Volume, DisplayClass.Spice);

    public Utensil Pan { get; } = new() { Name = "patelnia", NormalizedName = "patelnia" };

    public Utensil Oven { get; } = new() { Name = "piekarnik", NormalizedName = "piekarnik" };

    public Tag Quick { get; } = new() { Name = Tag.QuickTagName, NormalizedName = Tag.QuickTagName, IsSystem = true };

    public Tag Dinner { get; } = new() { Name = "obiad", NormalizedName = "obiad" };

    public async Task SeedCatalogAsync(AppDbContext db)
    {
        db.Products.AddRange(Chicken, Cream, HeavyCream, Pepper, Salt);
        db.Utensils.AddRange(Pan, Oven);
        db.Tags.AddRange(Quick, Dinner);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>"Kurczak w śmietanie": chicken 600 g, cream 200 ml, salt to taste; one prep and two instruction steps.</summary>
    public async Task<Recipe> AddRecipeAsync(AppDbContext db, Guid authorId, VisibilityLevel visibility, string title = "Kurczak w śmietanie")
    {
        var recipe = new Recipe
        {
            AuthorId = authorId,
            Title = title,
            NormalizedTitle = NameNormalizer.Normalize(title),
            Visibility = visibility,
            PrepMinutes = 10,
            CookMinutes = 40,
            CreatedAt = TestData.Now,
            UpdatedAt = TestData.Now,
        };
        recipe.Ingredients.Add(Ingredient(recipe, 0, Chicken, new Quantity(600, Unit.G), 600));
        recipe.Ingredients.Add(Ingredient(recipe, 1, Cream, new Quantity(200, Unit.Ml), 200));
        recipe.Ingredients.Add(Ingredient(recipe, 2, Salt, null, null));
        recipe.Steps.Add(new RecipeStep { RecipeId = recipe.Id, Section = StepSection.Prep, Position = 0, Blocks = [new TextBlock("Pokrój kurczaka.")] });
        recipe.Steps.Add(new RecipeStep { RecipeId = recipe.Id, Section = StepSection.Instructions, Position = 0, Blocks = [new TextBlock("Usmaż kurczaka.")] });
        recipe.Steps.Add(new RecipeStep { RecipeId = recipe.Id, Section = StepSection.Instructions, Position = 1, Blocks = [new TextBlock("Dodaj śmietanę.")] });
        recipe.Utensils.Add(new RecipeUtensil { RecipeId = recipe.Id, UtensilId = Pan.Id });
        recipe.Tags.Add(new RecipeTag { RecipeId = recipe.Id, TagId = Dinner.Id });
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return recipe;
    }

    /// <summary>Adds members to the fridge that dev-login created for <paramref name="ownerId"/>; returns its id.</summary>
    public static async Task<Guid> ShareFridgeAsync(AppDbContext db, Guid ownerId, params Guid[] memberIds)
    {
        var ct = TestContext.Current.CancellationToken;
        var fridge = await db.Fridges.FirstOrDefaultAsync(f => f.OwnerId == ownerId, ct);
        if (fridge is null)
        {
            fridge = new Fridge { Name = "Dom", OwnerId = ownerId, CreatedAt = TestData.Now };
            fridge.Members.Add(new FridgeMember { FridgeId = fridge.Id, UserId = ownerId, Role = FridgeRole.Owner, JoinedAt = TestData.Now });
            db.Fridges.Add(fridge);
        }

        foreach (var member in memberIds)
        {
            db.FridgeMembers.Add(new FridgeMember { FridgeId = fridge.Id, UserId = member, Role = FridgeRole.Member, JoinedAt = TestData.Now });
        }

        await db.SaveChangesAsync(ct);
        return fridge.Id;
    }

    /// <summary>The unchanged edited copy of <paramref name="recipe"/>, as dictionaries the test can change.</summary>
    public static EditedCopy CopyOf(Recipe recipe) => new(recipe);

    /// <summary>A <c>VariantWrite</c> body.</summary>
    public static object Write(string name, EditedCopy copy, string visibility = "public", uint? version = null) =>
        version is { } v
            ? new { name, description = (string?)null, visibility, recipe = copy.Body(), version = v }
            : new { name, description = (string?)null, visibility, recipe = copy.Body() };

    private static Product Product(string name, MeasureKind kind, DisplayClass display, decimal? gramsPerMl = null, decimal? gramsPerPiece = null, bool hasSizes = false) => new()
    {
        Name = name,
        NormalizedName = NameNormalizer.Normalize(name),
        Category = ProductCategory.Other,
        MeasureKind = kind,
        DisplayClass = display,
        HasSizes = hasSizes,
        GramsPerMl = gramsPerMl,
        GramsPerPiece = gramsPerPiece,
        CreatedAt = TestData.Now,
    };

    private static RecipeIngredient Ingredient(Recipe recipe, int position, Product product, Quantity? quantity, decimal? baseAmount) => new()
    {
        RecipeId = recipe.Id,
        Position = position,
        ProductId = product.Id,
        Quantity = quantity,
        BaseAmount = baseAmount,
    };
}

/// <summary>A mutable edited copy (VariantWrite.recipe) of a base recipe.</summary>
internal sealed class EditedCopy
{
    public EditedCopy(Recipe recipe)
    {
        Ingredients = [.. recipe.Ingredients.OrderBy(i => i.Position).Select(i => new Dictionary<string, object?>
        {
            ["id"] = i.Id,
            ["productId"] = i.ProductId,
            ["quantity"] = i.Quantity is { } q ? Quantity(q.Value, q.Unit.ToString().ToLowerInvariant()) : null,
            ["note"] = i.Note,
            ["optional"] = i.Optional,
        })];
        PrepSteps = Steps(recipe, StepSection.Prep);
        InstructionSteps = Steps(recipe, StepSection.Instructions);
        UtensilIds = [.. recipe.Utensils.Select(u => u.UtensilId)];
        PrepMinutes = recipe.PrepMinutes;
        CookMinutes = recipe.CookMinutes;
    }

    public List<Dictionary<string, object?>> Ingredients { get; }

    public List<Dictionary<string, object?>> PrepSteps { get; }

    public List<Dictionary<string, object?>> InstructionSteps { get; }

    public List<Guid> UtensilIds { get; }

    public int? PrepMinutes { get; set; }

    public int? CookMinutes { get; set; }

    public static object Quantity(decimal value, string unit, string? size = null) => new { value, unit, size };

    public static Dictionary<string, object?> NewIngredient(Guid productId, object? quantity, Guid? id = null) => new()
    {
        ["id"] = id,
        ["productId"] = productId,
        ["quantity"] = quantity,
        ["note"] = null,
        ["optional"] = false,
    };

    public static Dictionary<string, object?> NewStep(string text, Guid? id = null) => new()
    {
        ["id"] = id,
        ["blocks"] = new object[] { new { type = "text", text } },
    };

    public object Body() => new
    {
        prepMinutes = PrepMinutes,
        cookMinutes = CookMinutes,
        utensilIds = UtensilIds,
        ingredients = Ingredients,
        prepSteps = PrepSteps,
        instructionSteps = InstructionSteps,
    };

    private static List<Dictionary<string, object?>> Steps(Recipe recipe, StepSection section) =>
        [.. recipe.Steps.Where(s => s.Section == section).OrderBy(s => s.Position).Select(s => new Dictionary<string, object?>
        {
            ["id"] = s.Id,
            ["blocks"] = s.Blocks.Select(b => b switch
            {
                TextBlock t => (object)new { type = "text", text = t.Text },
                ImageBlock i => new { type = "image", imageId = i.ImageId, caption = i.Caption },
                _ => throw new ArgumentOutOfRangeException(nameof(recipe)),
            }).ToArray(),
        })];
}
