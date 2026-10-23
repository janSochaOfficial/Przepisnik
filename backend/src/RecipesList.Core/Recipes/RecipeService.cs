using System.Transactions;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Recipes;

/// <summary>Commands on base recipes (API.md §4). Reads are projected by the API layer through <see cref="VisibilityRules"/>.</summary>
public interface IRecipeService
{
    /// <summary>Creates a recipe owned by the caller and returns its id.</summary>
    Task<Guid> CreateAsync(RecipeWriteInput input, VisibilityLevel visibility, CancellationToken ct);

    /// <summary>Edits a recipe directly (PROJECT §4.5): keeps ids of rows that are sent back, and may delete variants.</summary>
    Task UpdateAsync(Guid id, RecipeWriteInput input, uint version, bool deleteVariants, CancellationToken ct);

    /// <summary>Changes visibility; narrowing is blocked while other users have variants (PROJECT §4.4).</summary>
    Task SetVisibilityAsync(Guid id, VisibilityLevel visibility, CancellationToken ct);

    /// <summary>Deletes a recipe, or archives it when other users have variants (PROJECT §4.4).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}

/// <inheritdoc />
public sealed class RecipeService(
    IAppDbContext db,
    ICurrentUser currentUser,
    VisibilityRules rules,
    IQuantityConverter converter,
    SelectionMemory selections,
    TimeProvider time) : IRecipeService
{
    /// <inheritdoc />
    public async Task<Guid> CreateAsync(RecipeWriteInput input, VisibilityLevel visibility, CancellationToken ct)
    {
        RecipeInputValidator.Validate(input).ThrowIfAny();

        var now = time.GetUtcNow();
        var title = input.Title.Trim();
        var recipe = new Recipe
        {
            AuthorId = currentUser.Id,
            Title = title,
            NormalizedTitle = NameNormalizer.Normalize(title),
            Visibility = visibility,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await ApplyAsync(recipe, input, ct);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(ct);
        return recipe.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Guid id, RecipeWriteInput input, uint version, bool deleteVariants, CancellationToken ct)
    {
        var me = currentUser.Id;
        var recipe = await LoadForAuthorAsync(id, ct);
        EnsureNotArchived(recipe);

        if (recipe.Visibility == VisibilityLevel.Public)
        {
            throw new ConflictException(ErrorCodes.RecipePublicLocked, "A public recipe cannot be edited; create a variant instead.");
        }

        RecipeInputValidator.Validate(input).ThrowIfAny();

        if (recipe.Version != version)
        {
            throw new ConflictException(ErrorCodes.ConcurrencyConflict, "The recipe was changed by someone else.");
        }

        var counts = await db.Variants
            .Where(v => v.RecipeId == id)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Foreign = g.Count(v => v.AuthorId != me) })
            .SingleOrDefaultAsync(ct);

        if (counts is { Total: > 0 })
        {
            if (!deleteVariants)
            {
                throw new ConflictException(ErrorCodes.RecipeHasVariants, "The recipe has variants; saving deletes them.")
                    .With("variantCount", counts.Total)
                    .With("foreignVariantCount", counts.Foreign);
            }

            // Two saves in one local transaction (same connection): the variants must be gone from the database
            // before SelectionMemory looks at what is left of the remembered picks.
            using var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
            await DeleteVariantsAsync(recipe, ct);
            await db.SaveChangesAsync(ct);
            await ApplyAsync(recipe, input, ct);
            recipe.UpdatedAt = time.GetUtcNow();
            await selections.DropStalePicksAsync(recipe, excludeVariantId: null, ct);
            await db.SaveChangesAsync(ct);
            scope.Complete();
            return;
        }

        await ApplyAsync(recipe, input, ct);
        recipe.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetVisibilityAsync(Guid id, VisibilityLevel visibility, CancellationToken ct)
    {
        var me = currentUser.Id;
        var recipe = await rules.VisibleRecipes(me).SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        EnsureAuthor(recipe);
        EnsureNotArchived(recipe);

        if (visibility < recipe.Visibility)
        {
            var foreign = await db.Variants.CountAsync(v => v.RecipeId == id && v.AuthorId != me, ct);
            if (foreign > 0)
            {
                throw new ConflictException(ErrorCodes.RecipeVisibilityLocked, "Other users have variants of this recipe.")
                    .With("foreignVariantCount", foreign);
            }
        }

        if (visibility != recipe.Visibility)
        {
            recipe.Visibility = visibility;
            recipe.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var me = currentUser.Id;
        var recipe = await rules.VisibleRecipes(me).SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        EnsureAuthor(recipe);

        if (await db.Variants.AnyAsync(v => v.RecipeId == id && v.AuthorId != me, ct))
        {
            if (!recipe.IsArchived)
            {
                recipe.IsArchived = true;
                recipe.UpdatedAt = time.GetUtcNow();
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        // Variants, favourites and remembered picks go with the recipe (database cascade).
        db.Recipes.Remove(recipe);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Recipe> LoadForAuthorAsync(Guid id, CancellationToken ct)
    {
        var recipe = await rules.VisibleRecipes(currentUser.Id).SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        EnsureAuthor(recipe);

        // Children are loaded one query each (no cartesian product) and attach to the tracked recipe.
        await db.RecipeIngredients.Where(i => i.RecipeId == id).LoadAsync(ct);
        await db.RecipeSteps.Where(s => s.RecipeId == id).LoadAsync(ct);
        await db.RecipeTags.Where(t => t.RecipeId == id).LoadAsync(ct);
        await db.RecipeUtensils.Where(u => u.RecipeId == id).LoadAsync(ct);
        return recipe;
    }

    private void EnsureAuthor(Recipe recipe)
    {
        if (recipe.AuthorId != currentUser.Id)
        {
            throw new ForbiddenException(ErrorCodes.RecipeNotAuthor, "Only the author can change this recipe.");
        }
    }

    // Archived recipes are read-only, even for the author (D-27).
    private static void EnsureNotArchived(Recipe recipe)
    {
        if (recipe.IsArchived)
        {
            throw new ConflictException(ErrorCodes.RecipeArchived, "An archived recipe cannot be changed.");
        }
    }

    // All variants of the recipe and their favourites. Remembered picks are cleaned up by SelectionMemory afterwards.
    private async Task DeleteVariantsAsync(Recipe recipe, CancellationToken ct)
    {
        var variants = await db.Variants.Where(v => v.RecipeId == recipe.Id).ToListAsync(ct);
        var variantIds = variants.Select(v => v.Id).ToList();
        db.VariantFavorites.RemoveRange(await db.VariantFavorites.Where(f => variantIds.Contains(f.VariantId)).ToListAsync(ct));
        db.Variants.RemoveRange(variants);
    }

    // Copies the input onto the recipe: updates rows whose id is sent back, adds the others, removes the missing ones.
    private async Task ApplyAsync(Recipe recipe, RecipeWriteInput input, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var refs = await LoadReferencesAsync(recipe, input, errors, ct);
        CheckRowIds(recipe, input, errors);
        errors.ThrowIfAny();

        var title = input.Title.Trim();
        recipe.Title = title;
        recipe.NormalizedTitle = NameNormalizer.Normalize(title);
        recipe.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description;
        recipe.CoverImageId = input.CoverImageId;
        recipe.PrepMinutes = input.PrepMinutes;
        recipe.CookMinutes = input.CookMinutes;

        ApplyIngredients(recipe, input, refs.Products);
        ApplySteps(recipe, input);
        await ApplyTagsAsync(recipe, input, refs.TagIds, ct);
        ApplyUtensils(recipe, refs.UtensilIds);
    }

    private void ApplyIngredients(Recipe recipe, RecipeWriteInput input, Dictionary<Guid, Product> products)
    {
        var existing = recipe.Ingredients.ToDictionary(i => i.Id);
        var kept = new HashSet<Guid>();

        for (var position = 0; position < input.Ingredients.Count; position++)
        {
            var source = input.Ingredients[position];
            RecipeIngredient row;
            if (source.Id is { } id)
            {
                row = existing[id];
                kept.Add(id);
            }
            else
            {
                row = new RecipeIngredient { RecipeId = recipe.Id };
                db.RecipeIngredients.Add(row);
            }

            row.Position = position;
            row.ProductId = source.ProductId;
            row.Quantity = source.Quantity;
            row.BaseAmount = converter.ToBaseAmount(source.Quantity, products[source.ProductId]);
            row.Note = string.IsNullOrWhiteSpace(source.Note) ? null : source.Note;
            row.Optional = source.Optional;
        }

        foreach (var removed in recipe.Ingredients.Where(i => existing.ContainsKey(i.Id) && !kept.Contains(i.Id)).ToList())
        {
            db.RecipeIngredients.Remove(removed);
            recipe.Ingredients.Remove(removed);
        }
    }

    private void ApplySteps(Recipe recipe, RecipeWriteInput input)
    {
        var existing = recipe.Steps.ToDictionary(s => s.Id);
        var kept = new HashSet<Guid>();

        void Sync(StepSection section, IReadOnlyList<StepWriteInput> steps)
        {
            for (var position = 0; position < steps.Count; position++)
            {
                var source = steps[position];
                RecipeStep row;
                if (source.Id is { } id)
                {
                    row = existing[id];
                    kept.Add(id);
                }
                else
                {
                    row = new RecipeStep { RecipeId = recipe.Id };
                    db.RecipeSteps.Add(row);
                }

                row.Section = section;
                row.Position = position;
                row.Blocks = [.. source.Blocks];
            }
        }

        Sync(StepSection.Prep, input.PrepSteps);
        Sync(StepSection.Instructions, input.InstructionSteps);

        foreach (var removed in existing.Values.Where(s => !kept.Contains(s.Id)).ToList())
        {
            db.RecipeSteps.Remove(removed);
        }
    }

    private async Task ApplyTagsAsync(Recipe recipe, RecipeWriteInput input, HashSet<Guid> userTagIds, CancellationToken ct)
    {
        var wanted = new HashSet<Guid>(userTagIds);
        if (QuickTagRule.Applies(input.PrepMinutes, input.CookMinutes))
        {
            wanted.Add(await EnsureQuickTagAsync(ct));
        }

        foreach (var removed in recipe.Tags.Where(t => !wanted.Contains(t.TagId)).ToList())
        {
            db.RecipeTags.Remove(removed);
        }

        var present = recipe.Tags.Select(t => t.TagId).ToHashSet();
        foreach (var tagId in wanted.Where(t => !present.Contains(t)))
        {
            db.RecipeTags.Add(new RecipeTag { RecipeId = recipe.Id, TagId = tagId });
        }
    }

    // The seeder normally creates it; creating it here keeps the rule independent of seeding.
    private async Task<Guid> EnsureQuickTagAsync(CancellationToken ct)
    {
        var normalized = NameNormalizer.Normalize(Tag.QuickTagName);
        var existing = await db.Tags.Where(t => t.NormalizedName == normalized).Select(t => (Guid?)t.Id).SingleOrDefaultAsync(ct);
        if (existing is { } id)
        {
            return id;
        }

        var tag = new Tag { Name = Tag.QuickTagName, NormalizedName = normalized, IsSystem = true };
        db.Tags.Add(tag);
        return tag.Id;
    }

    private void ApplyUtensils(Recipe recipe, HashSet<Guid> utensilIds)
    {
        foreach (var removed in recipe.Utensils.Where(u => !utensilIds.Contains(u.UtensilId)).ToList())
        {
            db.RecipeUtensils.Remove(removed);
        }

        var present = recipe.Utensils.Select(u => u.UtensilId).ToHashSet();
        foreach (var utensilId in utensilIds.Where(u => !present.Contains(u)))
        {
            db.RecipeUtensils.Add(new RecipeUtensil { RecipeId = recipe.Id, UtensilId = utensilId });
        }
    }

    // Row ids that are sent back must belong to this recipe and appear once (API.md §4).
    private static void CheckRowIds(Recipe recipe, RecipeWriteInput input, ValidationErrors errors)
    {
        var ingredientIds = recipe.Ingredients.Select(i => i.Id).ToHashSet();
        var seen = new HashSet<Guid>();
        for (var i = 0; i < input.Ingredients.Count; i++)
        {
            if (input.Ingredients[i].Id is { } id && (!ingredientIds.Contains(id) || !seen.Add(id)))
            {
                errors.Add($"ingredients[{i}].id", "unknown_id");
            }
        }

        var stepIds = recipe.Steps.Select(s => s.Id).ToHashSet();
        seen.Clear();
        CheckStepIds("prepSteps", input.PrepSteps);
        CheckStepIds("instructionSteps", input.InstructionSteps);

        void CheckStepIds(string path, IReadOnlyList<StepWriteInput> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Id is { } id && (!stepIds.Contains(id) || !seen.Add(id)))
                {
                    errors.Add($"{path}[{i}].id", "unknown_id");
                }
            }
        }
    }

    private async Task<References> LoadReferencesAsync(Recipe recipe, RecipeWriteInput input, ValidationErrors errors, CancellationToken ct)
    {
        var me = currentUser.Id;

        var productIds = input.Ingredients.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        for (var i = 0; i < input.Ingredients.Count; i++)
        {
            var ingredient = input.Ingredients[i];
            if (!products.TryGetValue(ingredient.ProductId, out var product))
            {
                errors.Add($"ingredients[{i}].productId", ErrorCodes.ProductNotFound);
            }
            else if (ingredient.Quantity is { } quantity && quantity.Value > 0
                && converter.ToBaseAmount(quantity, product) is null)
            {
                errors.Add($"ingredients[{i}].quantity", ErrorCodes.QuantityUnitIncompatible);
            }
        }

        var tagIds = new HashSet<Guid>();
        var requestedTags = input.TagIds.Distinct().ToList();
        var tags = await db.Tags.Where(t => requestedTags.Contains(t.Id)).Select(t => new { t.Id, t.IsSystem }).ToListAsync(ct);
        for (var i = 0; i < input.TagIds.Count; i++)
        {
            var tag = tags.FirstOrDefault(t => t.Id == input.TagIds[i]);
            if (tag is null)
            {
                errors.Add($"tagIds[{i}]", ErrorCodes.ValidationFailed);
            }
            else if (!tag.IsSystem)
            {
                tagIds.Add(tag.Id);
            }
        }

        var utensilIds = input.UtensilIds.Distinct().ToHashSet();
        var knownUtensils = await db.Utensils.Where(u => utensilIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        for (var i = 0; i < input.UtensilIds.Count; i++)
        {
            if (!knownUtensils.Contains(input.UtensilIds[i]))
            {
                errors.Add($"utensilIds[{i}]", ErrorCodes.ValidationFailed);
            }
        }

        await CheckImagesAsync(recipe, input, errors, me, ct);
        return new References(products, tagIds, utensilIds);
    }

    // Images may be referenced by their uploader, or when the recipe already references them (API.md §3).
    private async Task CheckImagesAsync(Recipe recipe, RecipeWriteInput input, ValidationErrors errors, Guid me, CancellationToken ct)
    {
        var references = new List<(string Path, Guid ImageId)>();
        if (input.CoverImageId is { } cover)
        {
            references.Add(("coverImageId", cover));
        }

        AddBlockReferences("prepSteps", input.PrepSteps);
        AddBlockReferences("instructionSteps", input.InstructionSteps);

        void AddBlockReferences(string path, IReadOnlyList<StepWriteInput> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                for (var j = 0; j < steps[i].Blocks.Count; j++)
                {
                    if (steps[i].Blocks[j] is ImageBlock image)
                    {
                        references.Add(($"{path}[{i}].blocks[{j}].imageId", image.ImageId));
                    }
                }
            }
        }

        if (references.Count == 0)
        {
            return;
        }

        var ids = references.Select(r => r.ImageId).Distinct().ToList();
        var owned = await db.Images.Where(i => ids.Contains(i.Id) && i.OwnerId == me).Select(i => i.Id).ToListAsync(ct);
        var allowed = owned.ToHashSet();

        if (recipe.CoverImageId is { } currentCover)
        {
            allowed.Add(currentCover);
        }

        foreach (var block in recipe.Steps.SelectMany(s => s.Blocks).OfType<ImageBlock>())
        {
            allowed.Add(block.ImageId);
        }

        foreach (var (path, imageId) in references.Where(r => !allowed.Contains(r.ImageId)))
        {
            errors.Add(path, ErrorCodes.ImageNotOwned);
        }
    }

    private sealed record References(Dictionary<Guid, Product> Products, HashSet<Guid> TagIds, HashSet<Guid> UtensilIds);
}
