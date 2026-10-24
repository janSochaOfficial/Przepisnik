using System.Transactions;
using Microsoft.EntityFrameworkCore;
using RecipesList.Api.Features.Catalog;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Images;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Transfer;
using RecipesList.Core.Units;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Transfer;

/// <summary>
/// Creates a private recipe and private variants owned by the caller from an export file (PROJECT §4.7).
/// Everything happens in one transaction; photos are stored first and removed again when the import fails.
/// Products, tags and utensils are matched by normalized name through the catalog code, and created when missing.
/// </summary>
internal sealed class RecipeImporter(
    IAppDbContext db,
    ICurrentUser currentUser,
    IRecipeService recipes,
    IImageStorage storage,
    ProductCatalog productCatalog,
    NameCatalogs nameCatalogs,
    IQuantityConverter converter,
    TimeProvider time)
{
    /// <summary>Returns the id of the new recipe. Any invalid content is reported as 400 <c>import.invalid_format</c>.</summary>
    public async Task<Guid> ImportAsync(RecipeExportDocument document, CancellationToken ct)
    {
        ExportValidator.EnsureValid(document);
        var images = DecodeImages(document);

        var storedIds = new List<Guid>();
        try
        {
            using var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
            var imageIds = await StoreImagesAsync(images, storedIds, ct);
            var products = await ResolveProductsAsync(document, ct);
            var units = await ResolveUnitsAsync(document, products, ct);
            var recipeId = await CreateRecipeAsync(document, imageIds, products, units, ct);
            await CreateVariantsAsync(document, recipeId, imageIds, new FileRefs(products, units), ct);
            scope.Complete();
            return recipeId;
        }
        catch (DomainValidationException e) when (e.Code != ErrorCodes.ImportInvalidFormat && e.Code != ErrorCodes.ImportUnsupportedVersion)
        {
            await DeleteStoredAsync(storedIds);
            throw new DomainValidationException(ErrorCodes.ImportInvalidFormat, e.Errors, e.Message);
        }
        catch (ImageTooLargeException)
        {
            await DeleteStoredAsync(storedIds);
            throw new DomainValidationException(
                ErrorCodes.ImportInvalidFormat,
                new Dictionary<string, string[]> { ["images"] = ["too_large"] });
        }
        catch
        {
            await DeleteStoredAsync(storedIds);
            throw;
        }
    }

    // Only the photos that something references; the rest of the file's images are ignored.
    private static Dictionary<string, byte[]> DecodeImages(RecipeExportDocument document) =>
        ExportValidator.ReferencedImageKeys(document).ToDictionary(
            key => key,
            key => Convert.FromBase64String(document.Images[key].DataBase64),
            StringComparer.Ordinal);

    private async Task<Dictionary<string, Guid>> StoreImagesAsync(Dictionary<string, byte[]> images, List<Guid> storedIds, CancellationToken ct)
    {
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var now = time.GetUtcNow();
        foreach (var (key, bytes) in images)
        {
            await using var stream = new MemoryStream(bytes, writable: false);
            StoredImage stored;
            try
            {
                stored = await storage.SaveAsync(stream, ct);
            }
            catch (DomainValidationException)
            {
                throw new DomainValidationException(
                    ErrorCodes.ImportInvalidFormat,
                    new Dictionary<string, string[]> { [$"images.{key}"] = ["invalid"] });
            }

            storedIds.Add(stored.Id);
            ids[key] = stored.Id;
            db.Images.Add(new Image
            {
                Id = stored.Id,
                OwnerId = currentUser.Id,
                Width = stored.Width,
                Height = stored.Height,
                SizeBytes = stored.SizeBytes,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return ids;
    }

    private async Task DeleteStoredAsync(List<Guid> storedIds)
    {
        foreach (var id in storedIds)
        {
            try
            {
                await storage.DeleteAsync(id, CancellationToken.None);
            }
            catch (IOException)
            {
                // The orphan cleanup job removes what is left.
            }
        }
    }

    private async Task<Dictionary<string, Product>> ResolveProductsAsync(RecipeExportDocument document, CancellationToken ct)
    {
        var normalized = document.Products.ToDictionary(p => p.Key, p => NameNormalizer.Normalize(ProductRules.CleanName(p.Name)), StringComparer.Ordinal);
        var names = normalized.Values.ToList();
        var existing = await db.Products.Where(p => names.Contains(p.NormalizedName)).ToDictionaryAsync(p => p.NormalizedName, StringComparer.Ordinal, ct);

        var result = new Dictionary<string, Product>(StringComparer.Ordinal);
        foreach (var product in document.Products)
        {
            if (!existing.TryGetValue(normalized[product.Key], out var match))
            {
                match = await productCatalog.CreateAsync(
                    new ProductInput(
                        product.Name, product.Category, product.MeasureKind, product.DisplayClass, product.HasSizes, product.AllowHalf,
                        product.GramsPerMl, product.GramsPerPiece, product.DefaultPackageAmount, product.PieceForms),
                    ct);
            }

            result[product.Key] = match;
        }

        return result;
    }

    /// <summary>
    /// Reuses a unit with the same normalized name, scope, measure kind and an amount within 1 % (API.md §8), or creates
    /// one, renamed " (2)", " (3)" … when the name is taken in its scope. Saved here: recipe validation looks units up.
    /// </summary>
    private async Task<Dictionary<string, CustomUnit>> ResolveUnitsAsync(RecipeExportDocument document, Dictionary<string, Product> products, CancellationToken ct)
    {
        var result = new Dictionary<string, CustomUnit>(StringComparer.Ordinal);
        var fileUnits = document.CustomUnits ?? [];
        if (fileUnits.Count == 0)
        {
            return result;
        }

        var scopeIds = fileUnits.Select(u => u.ProductKey).OfType<string>().Select(k => products[k].Id).Distinct().ToList();
        var known = await db.CustomUnits.AsNoTracking()
            .Where(u => u.ProductId == null || scopeIds.Contains(u.ProductId.Value))
            .ToListAsync(ct);

        var now = time.GetUtcNow();
        foreach (var source in fileUnits)
        {
            Guid? scope = source.ProductKey is { } productKey ? products[productKey].Id : null;
            var name = ProductRules.CleanName(source.Name);
            var normalized = NameNormalizer.Normalize(name);
            var inScope = known.Where(u => u.ProductId == scope).ToList();
            var match = inScope.FirstOrDefault(u =>
                u.NormalizedName == normalized && u.MeasureKind == source.MeasureKind && CustomUnitRules.IsSameAmount(u.Amount, source.Amount));
            if (match is null)
            {
                var unique = CustomUnitRules.UniqueName(name, inScope.Select(u => u.NormalizedName).ToHashSet(StringComparer.Ordinal));
                match = new CustomUnit
                {
                    Name = unique,
                    NormalizedName = NameNormalizer.Normalize(unique),
                    Forms = new PluralForms(
                        ProductRules.CleanName(source.Forms.One),
                        ProductRules.CleanName(source.Forms.Few),
                        ProductRules.CleanName(source.Forms.Many),
                        ProductRules.CleanName(source.Forms.Fraction)),
                    ProductId = scope,
                    MeasureKind = source.MeasureKind,
                    Amount = QuantityConverter.RoundBase(source.Amount),
                    CreatedById = currentUser.Id,
                    CreatedAt = now,
                };
                db.CustomUnits.Add(match);
                known.Add(match);
            }

            result[source.Key] = match;
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    private static Quantity? ToQuantity(ExportQuantity? q, Dictionary<string, CustomUnit> units) => q is null
        ? null
        : new Quantity(q.Value, q.Unit, q.Size, q.CustomUnitKey is { } key ? units[key].Id : null);

    private async Task<List<Guid>> ResolveTagsAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var quick = NameNormalizer.Normalize(Tag.QuickTagName);
        var ids = new List<Guid>();
        foreach (var name in names)
        {
            // The system tag is computed from the times and never imported.
            if (NameNormalizer.Normalize(name) == quick)
            {
                continue;
            }

            var tag = (await nameCatalogs.CreateTagAsync(name, ct)).Entity;
            if (!ids.Contains(tag.Id))
            {
                ids.Add(tag.Id);
            }
        }

        return ids;
    }

    private async Task<List<Guid>> ResolveUtensilsAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var utensil = (await nameCatalogs.CreateUtensilAsync(name, ct)).Entity;
            if (!ids.Contains(utensil.Id))
            {
                ids.Add(utensil.Id);
            }
        }

        return ids;
    }

    private async Task<Guid> CreateRecipeAsync(
        RecipeExportDocument document,
        Dictionary<string, Guid> imageIds,
        Dictionary<string, Product> products,
        Dictionary<string, CustomUnit> units,
        CancellationToken ct)
    {
        var recipe = document.Recipe;
        var input = new RecipeWriteInput(
            recipe.Title,
            ImportFooter.Append(recipe.Description, recipe.AuthorNickname, RecipeInputValidator.MaxDescription),
            recipe.CoverImageKey is { } cover ? imageIds[cover] : null,
            recipe.PrepMinutes,
            recipe.CookMinutes,
            await ResolveTagsAsync(recipe.Tags, ct),
            await ResolveUtensilsAsync(recipe.Utensils, ct),
            [.. recipe.Ingredients.Select(i => new IngredientWriteInput(null, products[i.ProductKey].Id, ToQuantity(i.Quantity, units), i.Note, i.Optional))],
            [.. recipe.PrepSteps.Select(s => new StepWriteInput(null, Blocks(s.Blocks, imageIds)))],
            [.. recipe.InstructionSteps.Select(s => new StepWriteInput(null, Blocks(s.Blocks, imageIds)))]);

        return await recipes.CreateAsync(input, VisibilityLevel.Private, ct);
    }

    private async Task CreateVariantsAsync(
        RecipeExportDocument document,
        Guid recipeId,
        Dictionary<string, Guid> imageIds,
        FileRefs refs,
        CancellationToken ct)
    {
        if (document.Variants.Count == 0)
        {
            return;
        }

        // The recipe was created from the file's rows in order, so rows map back to keys by position.
        var ingredientRows = await db.RecipeIngredients.AsNoTracking().Where(i => i.RecipeId == recipeId).OrderBy(i => i.Position).Select(i => i.Id).ToListAsync(ct);
        var steps = await db.RecipeSteps.AsNoTracking().Where(s => s.RecipeId == recipeId).Select(s => new { s.Id, s.Section, s.Position }).ToListAsync(ct);
        var stepRows = steps.OrderBy(s => s.Section).ThenBy(s => s.Position).Select(s => s.Id).ToList();
        var baseIngredients = document.Recipe.Ingredients.Zip(ingredientRows, (e, id) => (e.Key, id)).ToDictionary(x => x.Key, x => x.id, StringComparer.Ordinal);
        var baseSteps = document.Recipe.PrepSteps.Concat(document.Recipe.InstructionSteps).Zip(stepRows, (e, id) => (e.Key, id)).ToDictionary(x => x.Key, x => x.id, StringComparer.Ordinal);

        var now = time.GetUtcNow();
        var takenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < document.Variants.Count; index++)
        {
            var source = document.Variants[index];
            var created = now.AddMicroseconds(index);
            var variant = new Variant
            {
                RecipeId = recipeId,
                AuthorId = currentUser.Id,
                Name = UniqueName(source.Name.Trim(), takenNames),
                Description = ImportFooter.Append(source.Description, source.AuthorNickname, ExportValidator.MaxDescription),
                Visibility = VisibilityLevel.Private,
                PrepMinutes = source.PrepMinutes,
                CookMinutes = source.CookMinutes,
                UtensilIdsAdded = await ResolveUtensilsAsync(source.UtensilsAdded, ct),
                UtensilIdsRemoved = await ResolveUtensilsAsync(source.UtensilsRemoved, ct),
                CreatedAt = created,
                UpdatedAt = created,
            };

            var addedIngredients = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var addedSteps = new Dictionary<string, Guid>(StringComparer.Ordinal);
            variant.IngredientOps = [.. source.IngredientOps.Select((op, i) => IngredientOp(op, i, baseIngredients, addedIngredients, refs))];
            variant.StepOps = [.. source.StepOps.Select(op => StepOp(op, baseSteps, addedSteps, imageIds))];
            db.Variants.Add(variant);
        }

        await db.SaveChangesAsync(ct);
    }

    // Variant names are unique per recipe and author, but a file may carry the same name from different authors.
    private static string UniqueName(string name, HashSet<string> taken)
    {
        var candidate = name;
        for (var n = 2; !taken.Add(candidate); n++)
        {
            var suffix = $" ({n})";
            candidate = (name.Length + suffix.Length > ExportValidator.MaxVariantName ? name[..(ExportValidator.MaxVariantName - suffix.Length)] : name) + suffix;
        }

        return candidate;
    }

    private IngredientOp IngredientOp(
        ExportIngredientOp op,
        int index,
        Dictionary<string, Guid> baseIngredients,
        Dictionary<string, Guid> added,
        FileRefs refs)
    {
        var products = refs.Products;
        switch (op)
        {
            case ExportModifyIngredientOp m:
                var modified = ToQuantity(m.Quantity, refs.Units);
                return new ModifyIngredientOp(
                    baseIngredients[m.TargetKey], products[m.ProductKey].Id, modified, BaseAmount(modified, products[m.ProductKey], refs, index), Clean(m.Note), m.Optional);
            case ExportRemoveIngredientOp r:
                return new RemoveIngredientOp(baseIngredients[r.TargetKey]);
            case ExportAddIngredientOp a:
                var id = Guid.CreateVersion7();
                added[a.Key] = id;
                var after = a.AfterKey is { } key ? (baseIngredients.TryGetValue(key, out var baseId) ? baseId : added[key]) : (Guid?)null;
                var addedQuantity = ToQuantity(a.Quantity, refs.Units);
                return new AddIngredientOp(id, after, products[a.ProductKey].Id, addedQuantity, BaseAmount(addedQuantity, products[a.ProductKey], refs, index), Clean(a.Note), a.Optional);
            default:
                throw new InvalidOperationException("Unknown ingredient operation.");
        }
    }

    private static StepOp StepOp(ExportStepOp op, Dictionary<string, Guid> baseSteps, Dictionary<string, Guid> added, Dictionary<string, Guid> imageIds)
    {
        switch (op)
        {
            case ExportReplaceStepOp r:
                return new ReplaceStepOp(r.Section, baseSteps[r.TargetKey], Blocks(r.Blocks, imageIds));
            case ExportRemoveStepOp r:
                return new RemoveStepOp(r.Section, baseSteps[r.TargetKey]);
            case ExportAddStepOp a:
                var id = Guid.CreateVersion7();
                added[a.Key] = id;
                var after = a.AfterKey is { } key ? (baseSteps.TryGetValue(key, out var baseId) ? baseId : added[key]) : (Guid?)null;
                return new AddStepOp(a.Section, id, after, Blocks(a.Blocks, imageIds));
            default:
                throw new InvalidOperationException("Unknown step operation.");
        }
    }

    // The server computes the canonical amount when an op is stored (VariantService does the same).
    private decimal? BaseAmount(Quantity? quantity, Product product, FileRefs refs, int opIndex)
    {
        if (quantity is null)
        {
            return null;
        }

        var unit = quantity.CustomUnitId is { } unitId ? refs.Units.Values.FirstOrDefault(u => u.Id == unitId) : null;
        var amount = quantity.IsWellFormed && (quantity.Size is null || product.HasSizes)
            ? converter.ToBaseAmount(quantity, product, unit)
            : null;
        return amount ?? throw new DomainValidationException(
            ErrorCodes.ImportInvalidFormat,
            new Dictionary<string, string[]> { [$"ingredientOps[{opIndex}].quantity"] = [ErrorCodes.QuantityUnitIncompatible] });
    }

    /// <summary>The catalog entries the file's keys resolved to.</summary>
    private sealed record FileRefs(Dictionary<string, Product> Products, Dictionary<string, CustomUnit> Units);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static List<ContentBlock> Blocks(IReadOnlyList<ExportBlock> blocks, Dictionary<string, Guid> imageIds) =>
        [.. blocks.Select(b => b switch
        {
            ExportTextBlock t => (ContentBlock)new TextBlock(t.Text),
            ExportImageBlock i => new ImageBlock(imageIds[i.ImageKey], i.Caption),
            _ => throw new InvalidOperationException("Unknown content block."),
        })];
}
