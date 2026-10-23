using System.Net;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Favorites;
using RecipesList.Core.Variants;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Recipes;

/// <summary>Create, read, update, delete and the computed fields of recipes (API.md §4).</summary>
public sealed class RecipeCrudTests(ApiFactory factory) : RecipeTestBase(factory)
{
    [Fact]
    public async Task Create_ThenGet_ReturnsTheRecipeWithPositionsAndBaseAmounts()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft("Placki", Ingredient(Flour, 200, "g"), Ingredient(Sugar), Ingredient(Butter, 1, "tbsp"));
        body["prepSteps"] = new[] { Step("Wymieszaj.") };
        body["tagIds"] = new[] { Dinner };
        body["utensilIds"] = new[] { Pan };

        var created = await CreateAsync(ola, body);
        var detail = await ContractAssert.IsJsonAsync(await GetRecipeAsync(ola, created.GetProperty("id").GetGuid()), HttpStatusCode.OK, "RecipeDetail");

        detail.GetProperty("title").GetString().ShouldBe("Placki");
        detail.GetProperty("visibility").GetString().ShouldBe("private");
        detail.GetProperty("canEdit").GetBoolean().ShouldBeTrue();
        detail.GetProperty("author").GetProperty("nickname").GetString().ShouldBe("ola");
        detail.GetProperty("isArchived").GetBoolean().ShouldBeFalse();
        detail.GetProperty("ingredients").EnumerateArray().Select(i => i.GetProperty("product").GetProperty("name").GetString())
            .ShouldBe(["Mąka", "Cukier", "Masło"]);
        var amounts = detail.GetProperty("ingredients").EnumerateArray().Select(i => i.GetProperty("baseAmount")).ToList();
        amounts[0].GetDecimal().ShouldBe(200m);
        amounts[1].ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        detail.GetProperty("prepSteps").GetArrayLength().ShouldBe(1);
        detail.GetProperty("utensils")[0].GetProperty("name").GetString().ShouldBe("patelnia");
        TagNames(detail).ShouldBe(["obiad"]);
    }

    [Fact]
    public async Task Create_ButterInTablespoons_StoresMillilitresTimesDensityAsBaseAmount()
    {
        using var ola = await LoginAsync("ola");

        var created = await CreateAsync(ola, Draft("Z masłem", Ingredient(Butter, 2, "tbsp")));

        // 2 tbsp = 30 ml; butter is a mass product with density 0.911 g/ml.
        created.GetProperty("ingredients")[0].GetProperty("baseAmount").GetDecimal().ShouldBe(27.33m);
        created.GetProperty("ingredients")[0].GetProperty("quantity").GetProperty("unit").GetString().ShouldBe("tbsp");
    }

    [Fact]
    public async Task Create_SpoonsOfFlourWithoutDensity_Returns400WithTheIngredientPath()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft("Zła mąka", Ingredient(Sugar, 1, "g"), Ingredient(Flour, 100, "g"), Ingredient(Flour, 2, "tbsp"));

        var problem = await ContractAssert.IsProblemAsync(await ola.PostJsonAsync("/api/v1/recipes", body), HttpStatusCode.BadRequest, "quantity.unit_incompatible");

        problem.GetProperty("errors").GetProperty("ingredients[2].quantity")[0].GetString().ShouldBe("quantity.unit_incompatible");
    }

    [Theory]
    [InlineData(10, 20, true)]
    [InlineData(10, 25, false)]
    [InlineData(null, 30, true)]
    [InlineData(31, null, false)]
    [InlineData(null, null, false)]
    public async Task Create_Times_SetTheQuickTagOnlyWhenTheTotalIsAtMostThirtyMinutes(int? prep, int? cook, bool quick)
    {
        using var ola = await LoginAsync("ola");
        var body = Draft();
        body["prepMinutes"] = prep;
        body["cookMinutes"] = cook;

        var created = await CreateAsync(ola, body);

        TagNames(created).Contains("szybkie").ShouldBe(quick);
        if (quick)
        {
            created.GetProperty("tags").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "szybkie")
                .GetProperty("isSystem").GetBoolean().ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Update_ChangingTimes_AddsAndRemovesTheQuickTag()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft();
        body["prepMinutes"] = 10;
        body["cookMinutes"] = 20;
        var created = await CreateAsync(ola, body);
        TagNames(created).ShouldContain("szybkie");

        var slower = UpdateBody(created);
        slower["cookMinutes"] = 25;
        var updated = await ContractAssert.IsJsonAsync(
            await ola.PutJsonAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}", slower), HttpStatusCode.OK, "RecipeDetail");
        TagNames(updated).ShouldNotContain("szybkie");

        var faster = UpdateBody(updated);
        faster["cookMinutes"] = 5;
        var again = await ContractAssert.IsJsonAsync(
            await ola.PutJsonAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}", faster), HttpStatusCode.OK, "RecipeDetail");
        TagNames(again).ShouldContain("szybkie");
    }

    [Fact]
    public async Task Create_QuickTagInTagIds_IsIgnoredWhenTheTimesDoNotQualify()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft();
        body["tagIds"] = new[] { QuickTagId, Vegan };

        var created = await CreateAsync(ola, body);

        TagNames(created).ShouldBe(["wege"]);
    }

    [Fact]
    public async Task Update_ReorderEditOneAddOneDeleteOne_KeepsTheIdsOfUntouchedRows()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft("Ciasto", Ingredient(Flour, 200, "g"), Ingredient(Sugar, 50, "g"), Ingredient(Butter, 100, "g"));
        body["instructionSteps"] = new[] { Step("Jeden."), Step("Dwa.") };
        var created = await CreateAsync(ola, body);
        var id = created.GetProperty("id").GetGuid();
        var flour = created.GetProperty("ingredients")[0].GetProperty("id").GetGuid();
        var sugar = created.GetProperty("ingredients")[1].GetProperty("id").GetGuid();
        var step1 = created.GetProperty("instructionSteps")[0].GetProperty("id").GetGuid();
        var step2 = created.GetProperty("instructionSteps")[1].GetProperty("id").GetGuid();

        // Butter is deleted, sugar moves first with a new amount, flour keeps its row, onion is new.
        var update = UpdateBody(created);
        update["ingredients"] = new[]
        {
            Ingredient(Sugar, 80, "g", sugar),
            Ingredient(Flour, 200, "g", flour),
            Ingredient(Onion, 1, "piece"),
        };
        update["instructionSteps"] = new[] { Step("Dwa.", step2), Step("Jeden.", step1) };
        var updated = await ContractAssert.IsJsonAsync(await ola.PutJsonAsync($"/api/v1/recipes/{id}", update), HttpStatusCode.OK, "RecipeDetail");

        var ingredients = Ids(updated.GetProperty("ingredients"));
        ingredients.Count.ShouldBe(3);
        ingredients[0].ShouldBe(sugar);
        ingredients[1].ShouldBe(flour);
        ingredients[2].ShouldNotBe(Guid.Empty);
        updated.GetProperty("ingredients")[0].GetProperty("baseAmount").GetDecimal().ShouldBe(80m);
        Ids(updated.GetProperty("instructionSteps")).ShouldBe([step2, step1]);
        updated.GetProperty("version").GetUInt32().ShouldNotBe(created.GetProperty("version").GetUInt32());

        await using var db = Factory.CreateDbContext();
        (await db.RecipeIngredients.CountAsync(i => i.RecipeId == id, TestContext.Current.CancellationToken)).ShouldBe(3);
    }

    [Fact]
    public async Task Update_UnknownRowId_Returns400()
    {
        using var ola = await LoginAsync("ola");
        var created = await CreateAsync(ola, Draft());
        var update = UpdateBody(created);
        update["ingredients"] = new[] { Ingredient(Flour, 1, "g", Guid.NewGuid()) };

        var problem = await ContractAssert.IsProblemAsync(
            await ola.PutJsonAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}", update), HttpStatusCode.BadRequest, "validation.failed");

        problem.GetProperty("errors").TryGetProperty("ingredients[0].id", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409Conflict()
    {
        using var ola = await LoginAsync("ola");
        var created = await CreateAsync(ola, Draft());
        var id = created.GetProperty("id").GetGuid();
        var first = UpdateBody(created);
        first["title"] = "Pierwsza zmiana";
        (await ola.PutJsonAsync($"/api/v1/recipes/{id}", first)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var stale = UpdateBody(created);
        stale["title"] = "Spóźniona zmiana";
        var response = await ola.PutJsonAsync($"/api/v1/recipes/{id}", stale);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Conflict, "concurrency.conflict");
    }

    [Fact]
    public async Task Update_PublicRecipe_Returns409PublicLocked()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft();
        body["visibility"] = "public";
        var created = await CreateAsync(ola, body);

        var response = await ola.PutJsonAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}", UpdateBody(created));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Conflict, "recipe.public_locked");
        created.GetProperty("canEdit").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Update_ByAnotherUserWhoCanSeeIt_Returns403NotAuthor()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft();
        body["visibility"] = "public";
        var created = await CreateAsync(ola, body);

        var response = await kasia.PutJsonAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}", UpdateBody(created));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Forbidden, "recipe.not_author");
        var seen = await ContractAssert.IsJsonAsync(await GetRecipeAsync(kasia, created.GetProperty("id").GetGuid()), HttpStatusCode.OK, "RecipeDetail");
        seen.GetProperty("canEdit").GetBoolean().ShouldBeFalse();
        seen.GetProperty("totalVariantCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Update_WithVariantsAndNoConfirmation_Returns409WithCounts_AndRetryDeletesTheVariantsFavouritesAndPicks()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft("Zupa", Ingredient(Flour, 100, "g"));
        var created = await CreateAsync(ola, body);
        var id = created.GetProperty("id").GetGuid();
        var flourRow = created.GetProperty("ingredients")[0].GetProperty("id").GetGuid();

        var ownVariant = await AddVariantAsync(id, ola.UserId, "Moja");
        var foreignVariant = await AddVariantAsync(
            id,
            kasia.UserId,
            "Kasi",
            ops: [new ModifyIngredientOp(flourRow, Sugar, null, null, null, false)]);
        await using (var db = Factory.CreateDbContext())
        {
            db.VariantFavorites.Add(new VariantFavorite { UserId = kasia.UserId, VariantId = foreignVariant, CreatedAt = TestData.Now });
            db.RecipeSelections.Add(new RecipeSelection
            {
                UserId = kasia.UserId,
                RecipeId = id,
                Picks = new Dictionary<string, SelectionPick>(StringComparer.Ordinal)
                {
                    [MatrixKeys.BaseSlot(flourRow)] = new(MatrixKeys.Option(Sugar, null), PickSource.User),
                },
            });
            db.ProductPreferences.Add(new ProductPreference { UserId = kasia.UserId, FromProductId = Flour, ToProductId = Sugar, Weight = 1 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var update = UpdateBody(created);
        update["title"] = "Zupa v2";
        var conflict = await ContractAssert.IsProblemAsync(await ola.PutJsonAsync($"/api/v1/recipes/{id}", update), HttpStatusCode.Conflict, "recipe.has_variants");
        conflict.GetProperty("variantCount").GetInt32().ShouldBe(2);
        conflict.GetProperty("foreignVariantCount").GetInt32().ShouldBe(1);
        (await Count(db => db.Variants.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(2);

        update["deleteVariants"] = true;
        var ok = await ContractAssert.IsJsonAsync(await ola.PutJsonAsync($"/api/v1/recipes/{id}", update), HttpStatusCode.OK, "RecipeDetail");

        ok.GetProperty("title").GetString().ShouldBe("Zupa v2");
        ok.GetProperty("totalVariantCount").GetInt32().ShouldBe(0);
        (await Count(db => db.Variants.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);
        (await Count(db => db.VariantFavorites.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);
        (await Count(db => db.ProductPreferences.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);
        await using var check = Factory.CreateDbContext();
        var selection = await check.RecipeSelections.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        (selection?.Picks ?? []).ShouldBeEmpty();
        ownVariant.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Update_WithOwnVariantsOnly_ReportsForeignCountZero()
    {
        using var ola = await LoginAsync("ola");
        var created = await CreateAsync(ola, Draft());
        var id = created.GetProperty("id").GetGuid();
        await AddVariantAsync(id, ola.UserId, "Moja");

        var conflict = await ContractAssert.IsProblemAsync(
            await ola.PutJsonAsync($"/api/v1/recipes/{id}", UpdateBody(created)), HttpStatusCode.Conflict, "recipe.has_variants");

        conflict.GetProperty("variantCount").GetInt32().ShouldBe(1);
        conflict.GetProperty("foreignVariantCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Create_ValidationFailures_Return400WithFieldPaths()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft("   ");
        body["instructionSteps"] = Array.Empty<object>();
        body["cookMinutes"] = 5000;

        var problem = await ContractAssert.IsProblemAsync(await ola.PostJsonAsync("/api/v1/recipes", body), HttpStatusCode.BadRequest, "validation.failed");

        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("title", out _).ShouldBeTrue();
        errors.TryGetProperty("instructionSteps", out _).ShouldBeTrue();
        errors.TryGetProperty("cookMinutes", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithoutVisibility_Returns400()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft();
        body.Remove("visibility");

        var response = await ola.PostJsonAsync("/api/v1/recipes", body);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Create_UnknownProduct_Returns400()
    {
        using var ola = await LoginAsync("ola");

        var problem = await ContractAssert.IsProblemAsync(
            await ola.PostJsonAsync("/api/v1/recipes", Draft("X", Ingredient(Guid.NewGuid(), 1, "g"))), HttpStatusCode.BadRequest, "validation.failed");

        problem.GetProperty("errors").TryGetProperty("ingredients[0].productId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_WithoutForeignVariants_RemovesTheRecipeForGood()
    {
        using var ola = await LoginAsync("ola");
        var created = await CreateAsync(ola, Draft());
        var id = created.GetProperty("id").GetGuid();
        await AddVariantAsync(id, ola.UserId, "Moja");

        var response = await ola.DeleteAsync($"/api/v1/recipes/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.IsProblemAsync(await GetRecipeAsync(ola, id), HttpStatusCode.NotFound, "recipe.not_found");
        (await Count(db => db.Variants.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);
    }

    [Fact]
    public async Task Delete_WithAForeignVariant_ArchivesTheRecipe()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft();
        body["visibility"] = "public";
        var created = await CreateAsync(ola, body);
        var id = created.GetProperty("id").GetGuid();
        await AddVariantAsync(id, kasia.UserId, "Kasi", VisibilityLevelPrivate());

        var response = await ola.DeleteAsync($"/api/v1/recipes/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var listed = await ContractAssert.IsJsonAsync(await ola.GetAsync("/api/v1/recipes"), HttpStatusCode.OK, "PagedRecipeSummary");
        listed.GetProperty("totalCount").GetInt32().ShouldBe(0);
        var kasiaList = await ContractAssert.IsJsonAsync(await kasia.GetAsync("/api/v1/recipes"), HttpStatusCode.OK, "PagedRecipeSummary");
        kasiaList.GetProperty("totalCount").GetInt32().ShouldBe(0);
        foreach (var reader in new[] { ola, kasia })
        {
            var archived = await ContractAssert.IsJsonAsync(await GetRecipeAsync(reader, id), HttpStatusCode.OK, "RecipeDetail");
            archived.GetProperty("isArchived").GetBoolean().ShouldBeTrue();
        }

        using var stranger = await LoginAsync("stranger");
        await ContractAssert.IsProblemAsync(await GetRecipeAsync(stranger, id), HttpStatusCode.NotFound, "recipe.not_found");
    }

    [Fact]
    public async Task Delete_ByAnotherUser_Returns403()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft();
        body["visibility"] = "public";
        var created = await CreateAsync(ola, body);

        var response = await kasia.DeleteAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}");

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Forbidden, "recipe.not_author");
    }

    private static RecipesList.Core.Visibility.VisibilityLevel VisibilityLevelPrivate() => RecipesList.Core.Visibility.VisibilityLevel.Private;

    private async Task<T> Count<T>(Func<RecipesList.Infrastructure.Persistence.AppDbContext, Task<T>> query)
    {
        await using var db = Factory.CreateDbContext();
        return await query(db);
    }
}
