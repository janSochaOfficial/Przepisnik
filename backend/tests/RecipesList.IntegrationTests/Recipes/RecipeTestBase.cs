using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Recipes;

/// <summary>Shared arrangement for the recipe endpoint tests: a small catalog and request builders.</summary>
public abstract class RecipeTestBase(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    protected Guid Butter { get; private set; }

    protected Guid Flour { get; private set; }

    protected Guid Sugar { get; private set; }

    protected Guid Onion { get; private set; }

    protected Guid Dinner { get; private set; }

    protected Guid Vegan { get; private set; }

    protected Guid QuickTagId { get; private set; }

    protected Guid Pan { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        await using var db = Factory.CreateDbContext();

        Butter = AddProduct(db, "Masło", MeasureKind.Mass, DisplayClass.Weight, gramsPerMl: 0.911m);
        Flour = AddProduct(db, "Mąka", MeasureKind.Mass, DisplayClass.Weight);
        Sugar = AddProduct(db, "Cukier", MeasureKind.Mass, DisplayClass.Weight);
        Onion = AddProduct(db, "Cebula", MeasureKind.Count, DisplayClass.Piece, gramsPerPiece: 150m, hasSizes: true);
        Dinner = AddTag(db, "obiad", system: false);
        Vegan = AddTag(db, "wege", system: false);
        QuickTagId = AddTag(db, "szybkie", system: true);
        var pan = new Utensil { Name = "patelnia", NormalizedName = "patelnia" };
        db.Utensils.Add(pan);
        Pan = pan.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected static Guid AddProduct(
        RecipesList.Infrastructure.Persistence.AppDbContext db,
        string name,
        MeasureKind kind,
        DisplayClass display,
        decimal? gramsPerMl = null,
        decimal? gramsPerPiece = null,
        bool hasSizes = false)
    {
        var product = new Product
        {
            Name = name,
            NormalizedName = NameNormalizer.Normalize(name),
            MeasureKind = kind,
            DisplayClass = display,
            GramsPerMl = gramsPerMl,
            GramsPerPiece = gramsPerPiece,
            HasSizes = hasSizes,
            CreatedAt = TestData.Now,
        };
        db.Products.Add(product);
        return product.Id;
    }

    protected static Guid AddTag(RecipesList.Infrastructure.Persistence.AppDbContext db, string name, bool system)
    {
        var tag = new Tag { Name = name, NormalizedName = NameNormalizer.Normalize(name), IsSystem = system };
        db.Tags.Add(tag);
        return tag.Id;
    }

    protected async Task<ApiClient> LoginAsync(string nickname)
    {
        var client = Factory.CreateApiClient();
        await client.LoginAsync(nickname);
        return client;
    }

    /// <summary>A valid recipe body: one ingredient, one instruction step.</summary>
    protected Dictionary<string, object?> Draft(string title = "Naleśniki", params object[] ingredients) => new()
    {
        ["title"] = title,
        ["description"] = null,
        ["coverImageId"] = null,
        ["prepMinutes"] = null,
        ["cookMinutes"] = null,
        ["tagIds"] = Array.Empty<Guid>(),
        ["utensilIds"] = Array.Empty<Guid>(),
        ["ingredients"] = ingredients.Length > 0 ? ingredients : [Ingredient(Flour, 200, "g")],
        ["prepSteps"] = Array.Empty<object>(),
        ["instructionSteps"] = new object[] { Step("Smaż.") },
        ["visibility"] = "private",
    };

    protected static object Ingredient(Guid productId, decimal? value = null, string? unit = null, Guid? id = null, string? note = null) => new
    {
        id,
        productId,
        quantity = value is null ? null : new { value, unit },
        note,
        optional = false,
    };

    protected static object Step(string text, Guid? id = null) => new
    {
        id,
        blocks = new object[] { new { type = "text", text } },
    };

    protected static async Task<JsonElement> CreateAsync(ApiClient client, Dictionary<string, object?> body)
    {
        var response = await client.PostJsonAsync("/api/v1/recipes", body);
        var created = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "RecipeDetail");
        response.Headers.Location.ShouldNotBeNull();
        return created;
    }

    /// <summary>Turns a recipe detail into an update body (same content, current version).</summary>
    protected static Dictionary<string, object?> UpdateBody(JsonElement detail, bool deleteVariants = false)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = detail.GetProperty("title").GetString(),
            ["description"] = detail.GetProperty("description"),
            ["coverImageId"] = detail.GetProperty("coverImage").ValueKind == JsonValueKind.Null ? null : detail.GetProperty("coverImage").GetProperty("id"),
            ["prepMinutes"] = detail.GetProperty("prepMinutes"),
            ["cookMinutes"] = detail.GetProperty("cookMinutes"),
            ["tagIds"] = detail.GetProperty("tags").EnumerateArray().Where(t => !t.GetProperty("isSystem").GetBoolean()).Select(t => t.GetProperty("id")).ToArray(),
            ["utensilIds"] = detail.GetProperty("utensils").EnumerateArray().Select(u => u.GetProperty("id")).ToArray(),
            ["ingredients"] = detail.GetProperty("ingredients").EnumerateArray().Select(i => (object)new
            {
                id = i.GetProperty("id"),
                productId = i.GetProperty("product").GetProperty("id"),
                quantity = i.GetProperty("quantity"),
                note = i.GetProperty("note"),
                optional = i.GetProperty("optional"),
            }).ToArray(),
            ["prepSteps"] = Steps(detail, "prepSteps"),
            ["instructionSteps"] = Steps(detail, "instructionSteps"),
            ["version"] = detail.GetProperty("version").GetUInt32(),
            ["deleteVariants"] = deleteVariants,
        };
        return body;
    }

    protected static string[] TagNames(JsonElement detail) =>
        [.. detail.GetProperty("tags").EnumerateArray().Select(t => t.GetProperty("name").GetString()!)];

    protected static List<Guid> Ids(JsonElement array) => [.. array.EnumerateArray().Select(e => e.GetProperty("id").GetGuid())];

    protected static Task<HttpResponseMessage> GetRecipeAsync(ApiClient client, Guid id) => client.GetAsync($"/api/v1/recipes/{id}");

    /// <summary>Adds a variant row directly (the variant endpoints belong to B4).</summary>
    protected async Task<Guid> AddVariantAsync(Guid recipeId, Guid authorId, string name, VisibilityLevel visibility = VisibilityLevel.Private, List<IngredientOp>? ops = null)
    {
        await using var db = Factory.CreateDbContext();
        var variant = new Variant
        {
            RecipeId = recipeId,
            AuthorId = authorId,
            Name = name,
            Visibility = visibility,
            IngredientOps = ops ?? [],
            CreatedAt = TestData.Now,
            UpdatedAt = TestData.Now,
        };
        db.Variants.Add(variant);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return variant.Id;
    }

    /// <summary>Adds members to the owner's fridge (every user starts with one; an owner may own only one).</summary>
    protected async Task<Guid> AddFridgeAsync(Guid ownerId, params Guid[] memberIds)
    {
        await using var db = Factory.CreateDbContext();
        var fridge = await db.Fridges.SingleAsync(f => f.OwnerId == ownerId, TestContext.Current.CancellationToken);
        foreach (var memberId in memberIds)
        {
            db.FridgeMembers.Add(new FridgeMember { FridgeId = fridge.Id, UserId = memberId, Role = FridgeRole.Member, JoinedAt = TestData.Now });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return fridge.Id;
    }

    protected async Task RemoveFromFridgeAsync(Guid fridgeId, Guid userId)
    {
        await using var db = Factory.CreateDbContext();
        var member = await db.FridgeMembers.SingleAsync(m => m.FridgeId == fridgeId && m.UserId == userId, TestContext.Current.CancellationToken);
        db.FridgeMembers.Remove(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static object[] Steps(JsonElement detail, string name) =>
        [.. detail.GetProperty(name).EnumerateArray().Select(s => (object)new { id = s.GetProperty("id"), blocks = s.GetProperty("blocks") })];
}
