using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;
using RecipesList.Core.Recipes;
using RecipesList.Core.ShoppingLists;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Catalog;

/// <summary>Admin edit and merge of products (PROJECT §7.3, API.md §2).</summary>
public sealed class ProductAdminTests(ApiFactory factory) : CatalogTestBase(factory)
{
    [Fact]
    public async Task Update_AsAdmin_ChangesTheProduct()
    {
        var id = await AddProductAsync("Cebula", category: ProductCategory.Other);
        using var admin = await LoginAdminAsync();
        var body = ProductBody("Cebula żółta", "vegetables");
        body["defaultPackageAmount"] = 1000;

        var product = await ContractAssert.IsJsonAsync(
            await admin.PutJsonAsync($"/api/v1/admin/products/{id}", body), HttpStatusCode.OK, "ProductRef");

        product.GetProperty("name").GetString().ShouldBe("Cebula żółta");
        product.GetProperty("category").GetString().ShouldBe("vegetables");
        await using var db = Factory.CreateDbContext();
        (await db.Products.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).NormalizedName.ShouldBe("cebula zolta");
    }

    [Fact]
    public async Task Update_KeepingTheSameName_IsNotADuplicate()
    {
        var id = await AddProductAsync("Cebula");
        using var admin = await LoginAdminAsync();

        await ContractAssert.IsJsonAsync(
            await admin.PutJsonAsync($"/api/v1/admin/products/{id}", ProductBody(" cebula ", "vegetables")), HttpStatusCode.OK, "ProductRef");
    }

    [Fact]
    public async Task Update_ToAnExistingName_Returns409WithTheExistingId()
    {
        var other = await AddProductAsync("Cebula");
        var id = await AddProductAsync("Czosnek");
        using var admin = await LoginAdminAsync();

        var problem = await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/products/{id}", ProductBody("cebula")), HttpStatusCode.Conflict, "product.duplicate");

        problem.GetProperty("existingId").GetGuid().ShouldBe(other);
    }

    [Fact]
    public async Task Update_MeasureKindOfAProductInUse_Returns400()
    {
        var flour = await AddProductAsync("Mąka");
        await using (var db = Factory.CreateDbContext())
        {
            var owner = TestData.User("ania");
            db.Users.Add(owner);
            var recipe = TestData.Recipe(owner, VisibilityLevel.Private);
            recipe.Ingredients.Add(new RecipeIngredient { RecipeId = recipe.Id, ProductId = flour, Position = 0 });
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var admin = await LoginAdminAsync();

        await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/products/{flour}", ProductBody("Mąka", "dryGoods", "volume", "liquid")),
            HttpStatusCode.BadRequest,
            "product.invalid_combination");
    }

    [Fact]
    public async Task Update_AsNormalUser_Returns403()
    {
        var id = await AddProductAsync("Cebula");
        using var client = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await client.PutJsonAsync($"/api/v1/admin/products/{id}", ProductBody("Cebula")), HttpStatusCode.Forbidden, "admin.required");
    }

    [Fact]
    public async Task Update_UnknownProduct_Returns404()
    {
        using var admin = await LoginAdminAsync();

        await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/products/{Guid.NewGuid()}", ProductBody("Cebula")), HttpStatusCode.NotFound, "product.not_found");
    }

    [Fact]
    public async Task Merge_AsNormalUser_Returns403_AndChangesNothing()
    {
        var source = await AddProductAsync("Cebula");
        var target = await AddProductAsync("Cebula biała");
        using var client = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync($"/api/v1/admin/products/{source}/merge", new { targetId = target }),
            HttpStatusCode.Forbidden,
            "admin.required");

        await using var db = Factory.CreateDbContext();
        (await db.Products.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task Merge_MixedMeasureKind_Returns400_AndChangesNothing()
    {
        var source = await AddProductAsync("Mleko", MeasureKind.Volume, DisplayClass.Liquid);
        var target = await AddProductAsync("Mąka");
        using var admin = await LoginAdminAsync();

        await ContractAssert.IsProblemAsync(
            await admin.PostJsonAsync($"/api/v1/admin/products/{source}/merge", new { targetId = target }),
            HttpStatusCode.BadRequest,
            "product.merge_incompatible");

        await using var db = Factory.CreateDbContext();
        (await db.Products.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task Merge_UnknownSourceOrTarget_Returns404()
    {
        var product = await AddProductAsync("Cebula");
        using var admin = await LoginAdminAsync();

        await ContractAssert.IsProblemAsync(
            await admin.PostJsonAsync($"/api/v1/admin/products/{Guid.NewGuid()}/merge", new { targetId = product }),
            HttpStatusCode.NotFound,
            "product.not_found");
        await ContractAssert.IsProblemAsync(
            await admin.PostJsonAsync($"/api/v1/admin/products/{product}/merge", new { targetId = Guid.NewGuid() }),
            HttpStatusCode.NotFound,
            "product.not_found");
    }

    [Fact]
    public async Task Merge_IntoItself_Returns400()
    {
        var product = await AddProductAsync("Cebula");
        using var admin = await LoginAdminAsync();

        await ContractAssert.IsProblemAsync(
            await admin.PostJsonAsync($"/api/v1/admin/products/{product}/merge", new { targetId = product }),
            HttpStatusCode.BadRequest,
            "validation.failed");
    }

    [Fact]
    public async Task Merge_RepointsEveryReference_AndDeletesTheSource()
    {
        var source = await AddProductAsync("Cebula");
        var target = await AddProductAsync("Cebula biała");
        var other = await AddProductAsync("Szalotka");
        var scene = await ArrangeReferencesAsync(source, target, other);
        using var admin = await LoginAdminAsync();

        var response = await admin.PostJsonAsync($"/api/v1/admin/products/{source}/merge", new { targetId = target });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var db = Factory.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;

        (await db.Products.AnyAsync(p => p.Id == source, ct)).ShouldBeFalse();
        (await db.Products.AnyAsync(p => p.Id == target, ct)).ShouldBeTrue();

        (await db.RecipeIngredients.SingleAsync(i => i.Id == scene.IngredientId, ct)).ProductId.ShouldBe(target);
        (await db.FridgeItems.SingleAsync(i => i.Id == scene.FridgeItemId, ct)).ProductId.ShouldBe(target);
        (await db.ShoppingListItems.SingleAsync(i => i.Id == scene.ShoppingItemId, ct)).ProductId.ShouldBe(target);

        var variant = await db.Variants.AsNoTracking().SingleAsync(v => v.Id == scene.VariantId, ct);
        variant.IngredientOps.Count.ShouldBe(4);
        variant.IngredientOps.OfType<ModifyIngredientOp>().Single().ProductId.ShouldBe(target);
        variant.IngredientOps.OfType<AddIngredientOp>().Select(o => o.ProductId).ShouldBe([other, target]);
        variant.IngredientOps.OfType<RemoveIngredientOp>().Single().TargetId.ShouldBe(scene.IngredientId);

        var selection = await db.RecipeSelections.AsNoTracking().SingleAsync(ct);
        selection.Picks["slot"].OptionKey.ShouldBe(MatrixKeys.Option(target, 100m));

        var deduction = await db.Deductions.AsNoTracking().SingleAsync(ct);
        deduction.Lines.Single().ProductId.ShouldBe(target);

        var preferences = await db.ProductPreferences.AsNoTracking().ToListAsync(ct);
        preferences.Count.ShouldBe(3);
        preferences.Single(p => p.UserId == scene.UserId && p.FromProductId == target && p.ToProductId == other).Weight.ShouldBe(5);
        preferences.Single(p => p.UserId == scene.UserId && p.FromProductId == other && p.ToProductId == target).Weight.ShouldBe(4);
        preferences.Single(p => p.UserId == scene.OtherUserId && p.FromProductId == target && p.ToProductId == other).Weight.ShouldBe(1);
    }

    [Fact]
    public async Task Merge_WhenAStatementFails_RollsEverythingBack()
    {
        var source = await AddProductAsync("Cebula");
        var target = await AddProductAsync("Cebula biała");
        var other = await AddProductAsync("Szalotka");
        var scene = await ArrangeReferencesAsync(source, target, other);

        // Deleting the target mid-way is impossible through the API, so break the last step instead: an extra reference
        // the merge does not know about makes the final DELETE of the source violate its foreign key.
        await using (var db = Factory.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE merge_probe (product_id uuid NOT NULL REFERENCES products (id))", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO merge_probe (product_id) VALUES ({source})", TestContext.Current.CancellationToken);
        }

        using var admin = await LoginAdminAsync();

        var response = await admin.PostJsonAsync($"/api/v1/admin/products/{source}/merge", new { targetId = target });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await using var check = Factory.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        (await check.Products.AnyAsync(p => p.Id == source, ct)).ShouldBeTrue();
        (await check.RecipeIngredients.SingleAsync(i => i.Id == scene.IngredientId, ct)).ProductId.ShouldBe(source);
        (await check.FridgeItems.SingleAsync(i => i.Id == scene.FridgeItemId, ct)).ProductId.ShouldBe(source);
        (await check.Variants.AsNoTracking().SingleAsync(v => v.Id == scene.VariantId, ct))
            .IngredientOps.OfType<ModifyIngredientOp>().Single().ProductId.ShouldBe(source);
        (await check.ProductPreferences.CountAsync(ct)).ShouldBe(4);

        await check.Database.ExecuteSqlRawAsync("DROP TABLE merge_probe", ct);
    }

    private sealed record Scene(Guid UserId, Guid OtherUserId, Guid IngredientId, Guid VariantId, Guid FridgeItemId, Guid ShoppingItemId);

    /// <summary>
    /// A recipe, a variant (modify, add and remove ops), a fridge item, a shopping item, a selection, a deduction and
    /// four preferences that all mention <paramref name="source"/>.
    /// </summary>
    private async Task<Scene> ArrangeReferencesAsync(Guid source, Guid target, Guid other)
    {
        await using var db = Factory.CreateDbContext();
        var user = TestData.User("ania");
        var otherUser = TestData.User("basia");
        db.Users.AddRange(user, otherUser);

        var recipe = TestData.Recipe(user, VisibilityLevel.Private);
        var ingredient = new RecipeIngredient { RecipeId = recipe.Id, ProductId = source, Position = 0 };
        recipe.Ingredients.Add(ingredient);
        db.Recipes.Add(recipe);

        var variant = TestData.Variant(recipe, user, VisibilityLevel.Private);
        variant.IngredientOps =
        [
            new ModifyIngredientOp(ingredient.Id, source, null, null, null, false),
            new AddIngredientOp(Guid.CreateVersion7(), null, other, null, null, null, false),
            new AddIngredientOp(Guid.CreateVersion7(), null, source, null, null, null, false),
            new RemoveIngredientOp(ingredient.Id),
        ];
        db.Variants.Add(variant);

        var fridge = TestData.Fridge("Lodówka", user);
        db.Fridges.Add(fridge);
        var fridgeItem = new FridgeItem
        {
            FridgeId = fridge.Id,
            ProductId = source,
            PackageAmount = 500,
            RemainingPercent = 100,
            AddedById = user.Id,
            CreatedAt = TestData.Now,
            UpdatedAt = TestData.Now,
        };
        db.FridgeItems.Add(fridgeItem);
        var shoppingItem = new ShoppingListItem { FridgeId = fridge.Id, ProductId = source, Amount = 200, AddedById = user.Id, CreatedAt = TestData.Now };
        db.ShoppingListItems.Add(shoppingItem);

        db.RecipeSelections.Add(new RecipeSelection
        {
            UserId = user.Id,
            RecipeId = recipe.Id,
            Picks = new Dictionary<string, SelectionPick>(StringComparer.Ordinal) { ["slot"] = new(MatrixKeys.Option(source, 100m), PickSource.User) },
            UpdatedAt = TestData.Now,
        });

        db.Deductions.Add(new Deduction
        {
            FridgeId = fridge.Id,
            UserId = user.Id,
            CreatedAt = TestData.Now,
            Lines = [new DeductionLine(source, 100, 100, 0, [])],
        });

        db.ProductPreferences.AddRange(
            new ProductPreference { UserId = user.Id, FromProductId = source, ToProductId = other, Weight = 2 },
            new ProductPreference { UserId = user.Id, FromProductId = target, ToProductId = other, Weight = 3 },
            new ProductPreference { UserId = user.Id, FromProductId = other, ToProductId = source, Weight = 4 },
            new ProductPreference { UserId = otherUser.Id, FromProductId = source, ToProductId = other, Weight = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Scene(user.Id, otherUser.Id, ingredient.Id, variant.Id, fridgeItem.Id, shoppingItem.Id);
    }

    [Fact]
    public async Task Merge_ADeletedSourceCannotBeFound_AfterwardsViaTheApi()
    {
        var source = await AddProductAsync("Cebula");
        var target = await AddProductAsync("Cebula biała");
        using var admin = await LoginAdminAsync();

        (await admin.PostJsonAsync($"/api/v1/admin/products/{source}/merge", new { targetId = target })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ContractAssert.IsProblemAsync(await admin.GetAsync($"/api/v1/products/{source}"), HttpStatusCode.NotFound, "product.not_found");
        (await ApiClient.ReadJsonAsync(await admin.GetAsync("/api/v1/products?q=cebula"))).GetArrayLength().ShouldBe(1);
    }
}
