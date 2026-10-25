using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Units;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Variants;

/// <summary>Variant CRUD, the resolved view and the matrix (API.md §5, §5.1). Every read is scoped by <see cref="VisibilityRules"/>.</summary>
public interface IVariantService
{
    /// <summary>Variants of a recipe visible to the caller, oldest first. 404 when the recipe is not visible.</summary>
    Task<IReadOnlyList<VariantSummaryModel>> ListForRecipeAsync(Guid recipeId, CancellationToken ct);

    /// <summary>The caller's own variants, newest first, including those whose base is no longer visible.</summary>
    Task<Page<VariantSummaryModel>> ListMineAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>A visible variant (the author always gets it).</summary>
    Task<VariantDetailModel> GetAsync(Guid id, CancellationToken ct);

    /// <summary>A visible variant applied to its (visible) base recipe.</summary>
    Task<ResolvedVariantModel> GetResolvedAsync(Guid id, CancellationToken ct);

    /// <summary>Creates a variant from an edited copy of a visible recipe (D-12).</summary>
    Task<VariantDetailModel> CreateAsync(Guid recipeId, VariantInput input, CancellationToken ct);

    /// <summary>Re-diffs an edited copy into the caller's variant.</summary>
    Task<VariantDetailModel> UpdateAsync(Guid id, VariantInput input, uint version, CancellationToken ct);

    /// <summary>Deletes the caller's variant and drops remembered picks that pointed at its options.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>The matrix of a visible recipe over the variants visible to the caller (≤ 3 SQL statements, plus one when an option uses a custom unit).</summary>
    Task<VariantMatrixResult> GetMatrixAsync(Guid recipeId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class VariantService(
    IAppDbContext db,
    ICurrentUser currentUser,
    VisibilityRules visibility,
    IVariantEngine engine,
    IQuantityConverter converter,
    SelectionMemory selections,
    TimeProvider clock) : IVariantService
{
    private const int MaxTextLength = 5000;
    private const int MaxCaptionLength = 300;
    private const int MaxNoteLength = 300;

    /// <inheritdoc />
    public async Task<IReadOnlyList<VariantSummaryModel>> ListForRecipeAsync(Guid recipeId, CancellationToken ct)
    {
        var userId = currentUser.Id;
        await EnsureRecipeVisibleAsync(recipeId, ct);

        var rows = await visibility.VisibleVariants(userId)
            .Where(v => v.RecipeId == recipeId)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .ToSummaryRows(db, visibility.VisibleRecipes(userId), userId)
            .AsNoTracking()
            .ToListAsync(ct);
        return [.. rows.Select(r => r.ToModel())];
    }

    /// <inheritdoc />
    public async Task<Page<VariantSummaryModel>> ListMineAsync(int page, int pageSize, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var mine = db.Variants.Where(v => v.AuthorId == userId);
        var total = await mine.CountAsync(ct);
        var rows = await mine
            .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToSummaryRows(db, visibility.VisibleRecipes(userId), userId)
            .AsNoTracking()
            .ToListAsync(ct);
        return new Page<VariantSummaryModel>([.. rows.Select(r => r.ToModel())], page, pageSize, total);
    }

    /// <inheritdoc />
    public async Task<VariantDetailModel> GetAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var row = await visibility.VisibleVariants(userId)
            .Where(v => v.Id == id)
            .ToSummaryRows(db, visibility.VisibleRecipes(userId), userId)
            .AsNoTracking()
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.VariantNotFound);
        var units = await db.LoadCustomUnitsAsync(CustomUnitLookup.QuantitiesOf(row.Variant.IngredientOps), ct);
        return new VariantDetailModel(row.ToModel(), row.Variant) { Units = units };
    }

    /// <inheritdoc />
    public async Task<ResolvedVariantModel> GetResolvedAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var variant = await visibility.VisibleVariants(userId).AsNoTracking().SingleOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.VariantNotFound);

        // The resolved view shows base content, so the base must still be visible (even to the variant's author).
        var recipe = await LoadBaseAsync(
            visibility.VisibleRecipes(userId).Where(r => r.Id == variant.RecipeId).Include(r => r.Author).Include(r => r.CoverImage),
            ct)
            ?? throw new NotFoundException(ErrorCodes.VariantNotFound);
        recipe.Tags = await db.RecipeTags.AsNoTracking().Where(t => t.RecipeId == recipe.Id).Include(t => t.Tag).ToListAsync(ct);

        var resolved = engine.Resolve(recipe, variant);
        var productIds = resolved.Ingredients.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var units = await db.LoadCustomUnitsAsync(resolved.Ingredients.Select(i => i.Quantity), ct);
        var utensilList = await db.Utensils.AsNoTracking().Where(u => resolved.UtensilIds.Contains(u.Id)).ToListAsync(ct);
        var utensilsById = utensilList.ToDictionary(u => u.Id);
        var isFavorite = await db.RecipeFavorites.AnyAsync(f => f.UserId == userId && f.RecipeId == recipe.Id, ct);
        var cookCount = await db.RecipeCookCounts.Where(c => c.RecipeId == recipe.Id).Select(c => c.Count).FirstOrDefaultAsync(ct);

        var tags = recipe.Tags.Select(t => t.Tag).OfType<Tag>().Where(t => !t.IsSystem).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        if (resolved.IsQuick)
        {
            var quickName = NameNormalizer.Normalize(Tag.QuickTagName);
            var quick = await db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.IsSystem && t.NormalizedName == quickName, ct);
            if (quick is not null)
            {
                tags.Insert(0, quick);
            }
        }

        return new ResolvedVariantModel(
            recipe,
            variant,
            resolved,
            isFavorite,
            cookCount,
            recipe.Author?.Nickname ?? "",
            tags,
            [.. resolved.UtensilIds.Where(utensilsById.ContainsKey).Select(u => utensilsById[u])],
            products)
        {
            Units = units,
        };
    }

    /// <inheritdoc />
    public async Task<VariantDetailModel> CreateAsync(Guid recipeId, VariantInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var userId = currentUser.Id;

        // A variant can be created on any recipe the caller can see, except an archived one (read-only, D-17).
        var recipe = await LoadBaseAsync(visibility.VisibleRecipes(userId).Where(r => r.Id == recipeId && !r.IsArchived), ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);

        var name = NormalizeName(input.Name);
        if (await db.Variants.AnyAsync(v => v.RecipeId == recipeId && v.AuthorId == userId && v.Name == name, ct))
        {
            throw new ConflictException(ErrorCodes.VariantNameTaken);
        }

        var diff = await DiffAsync(recipe, input.Recipe, userId, ct);
        var now = clock.GetUtcNow();
        var variant = new Variant
        {
            RecipeId = recipeId,
            AuthorId = userId,
            Name = name,
            Description = NormalizeText(input.Description),
            Visibility = input.Visibility,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(variant, diff);
        db.Variants.Add(variant);
        await SaveCatchingNameRaceAsync(variant.RecipeId, name, variant.Id, ct);

        return await GetAsync(variant.Id, ct);
    }

    /// <inheritdoc />
    public async Task<VariantDetailModel> UpdateAsync(Guid id, VariantInput input, uint version, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var userId = currentUser.Id;
        var variant = await LoadOwnVariantAsync(id, ct);

        // The author keeps the variant when the base becomes invisible to them, but can then only delete it (PROJECT §6.5).
        var recipe = await LoadBaseAsync(visibility.VisibleRecipes(userId).Where(r => r.Id == variant.RecipeId), ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);

        if (variant.Version != version)
        {
            throw new ConflictException(ErrorCodes.ConcurrencyConflict);
        }

        var name = NormalizeName(input.Name);
        if (await db.Variants.AnyAsync(v => v.RecipeId == variant.RecipeId && v.AuthorId == userId && v.Name == name && v.Id != id, ct))
        {
            throw new ConflictException(ErrorCodes.VariantNameTaken);
        }

        var diff = await DiffAsync(recipe, input.Recipe, userId, ct);
        variant.Name = name;
        variant.Description = NormalizeText(input.Description);
        variant.Visibility = input.Visibility;
        variant.UpdatedAt = clock.GetUtcNow();
        Apply(variant, diff);

        // Options of the old ops may be gone: drop remembered picks of them in the same SaveChanges.
        await selections.DropStalePicksAsync(recipe, excludeVariantId: null, ct);
        await SaveCatchingNameRaceAsync(variant.RecipeId, name, id, ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Saves; when a simultaneous request took the same name first (the unique index caught it after the pre-check),
    /// answers 409 <c>variant.name_taken</c> instead of a 500.
    /// </summary>
    private async Task SaveCatchingNameRaceAsync(Guid recipeId, string name, Guid variantId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e is not DbUpdateConcurrencyException)
        {
            var userId = currentUser.Id;
            if (await db.Variants.AsNoTracking().AnyAsync(v => v.RecipeId == recipeId && v.AuthorId == userId && v.Name == name && v.Id != variantId, ct))
            {
                throw new ConflictException(ErrorCodes.VariantNameTaken);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var variant = await LoadOwnVariantAsync(id, ct);
        db.Variants.Remove(variant);

        var recipe = await db.Recipes.Include(r => r.Ingredients).AsNoTracking().SingleOrDefaultAsync(r => r.Id == variant.RecipeId, ct);
        if (recipe is not null)
        {
            await selections.DropStalePicksAsync(recipe, excludeVariantId: id, ct);
        }

        // Favourites of the variant go with it (database cascade).
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<VariantMatrixResult> GetMatrixAsync(Guid recipeId, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var recipeFavorites = db.RecipeFavorites;
        var variantFavorites = db.VariantFavorites;
        var recipeCookCounts = db.RecipeCookCounts;
        var variantCookCounts = db.VariantCookCounts;

        // Statement 1: the recipe, its author, the caller's ♥ and the ingredients with their products.
        var head = await visibility.VisibleRecipes(userId)
            .Where(r => r.Id == recipeId)
            .Select(r => new
            {
                Recipe = r,
                AuthorNickname = r.Author!.Nickname,
                IsFavorite = recipeFavorites.Any(f => f.UserId == userId && f.RecipeId == r.Id),
                CookCount = recipeCookCounts.Where(c => c.RecipeId == r.Id).Select(c => c.Count).FirstOrDefault(),
                Ingredients = r.Ingredients.Select(i => new { Row = i, Product = i.Product! }).ToList(),
            })
            .AsNoTracking()
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);

        var recipe = head.Recipe;
        recipe.Ingredients = [.. head.Ingredients.Select(i => i.Row)];

        // Statement 2: the visible variants with their authors and the caller's ♥, oldest first (rank tie-breaker).
        var variants = await visibility.VisibleVariants(userId)
            .Where(v => v.RecipeId == recipeId)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => new
            {
                Variant = v,
                AuthorNickname = v.Author!.Nickname,
                IsFavorite = variantFavorites.Any(f => f.UserId == userId && f.VariantId == v.Id),
                CookCount = variantCookCounts.Where(c => c.VariantId == v.Id).Select(c => c.Count).FirstOrDefault(),
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var matrix = engine.BuildMatrix(recipe, [.. variants.Select(v => v.Variant)]);

        // Statement 3 (only when variants reference products the base does not use).
        var products = head.Ingredients.Select(i => i.Product).DistinctBy(p => p.Id).ToDictionary(p => p.Id);
        var missing = matrix.Slots
            .SelectMany(s => s.Options.Select(o => o.Ingredient?.ProductId).Append(s.ProductId))
            .OfType<Guid>()
            .Where(p => !products.ContainsKey(p))
            .Distinct()
            .ToList();
        if (missing.Count > 0)
        {
            foreach (var product in await db.Products.AsNoTracking().Where(p => missing.Contains(p.Id)).ToListAsync(ct))
            {
                products[product.Id] = product;
            }
        }

        var columns = new List<MatrixColumnModel>(variants.Count + 1)
        {
            new(null, recipe.Title, recipe.AuthorId, head.AuthorNickname ?? "", head.CookCount, recipe.CreatedAt, head.IsFavorite),
        };
        columns.AddRange(variants.Select(v => new MatrixColumnModel(
            v.Variant.Id, v.Variant.Name, v.Variant.AuthorId, v.AuthorNickname ?? "", v.CookCount, v.Variant.CreatedAt, v.IsFavorite)));

        // Statement 4 (only when an option uses a custom unit): every such unit at once.
        var units = await db.LoadCustomUnitsAsync(matrix.Slots.SelectMany(s => s.Options).Select(o => o.Ingredient?.Quantity), ct);

        return new VariantMatrixResult(matrix, columns, products) { Units = units };
    }

    private async Task EnsureRecipeVisibleAsync(Guid recipeId, CancellationToken ct)
    {
        if (!await visibility.VisibleRecipes(currentUser.Id).AnyAsync(r => r.Id == recipeId, ct))
        {
            throw new NotFoundException(ErrorCodes.RecipeNotFound);
        }
    }

    // One query per collection: a single query with three collection includes would multiply their rows.
    private async Task<Recipe?> LoadBaseAsync(IQueryable<Recipe> query, CancellationToken ct)
    {
        var recipe = await query.AsNoTracking().SingleOrDefaultAsync(ct);
        if (recipe is null)
        {
            return null;
        }

        recipe.Ingredients = await db.RecipeIngredients.AsNoTracking().Where(i => i.RecipeId == recipe.Id).OrderBy(i => i.Position).ToListAsync(ct);
        recipe.Steps = await db.RecipeSteps.AsNoTracking().Where(s => s.RecipeId == recipe.Id).OrderBy(s => s.Position).ToListAsync(ct);
        recipe.Utensils = await db.RecipeUtensils.AsNoTracking().Where(u => u.RecipeId == recipe.Id).ToListAsync(ct);
        return recipe;
    }

    // 404 when the caller cannot see the variant, 403 when they can but are not its author (TECHNICAL §3.5).
    private async Task<Variant> LoadOwnVariantAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var variant = await db.Variants.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (variant is null)
        {
            throw new NotFoundException(ErrorCodes.VariantNotFound);
        }

        if (variant.AuthorId == userId)
        {
            return variant;
        }

        throw await visibility.VisibleVariants(userId).AnyAsync(v => v.Id == id, ct)
            ? new ForbiddenException(ErrorCodes.VariantNotAuthor)
            : new NotFoundException(ErrorCodes.VariantNotFound);
    }

    private static void Apply(Variant variant, VariantDiff diff)
    {
        if (diff.IsEmpty)
        {
            throw new DomainValidationException(ErrorCodes.VariantNoChanges);
        }

        variant.IngredientOps = [.. diff.IngredientOps];
        variant.StepOps = [.. diff.StepOps];
        variant.UtensilIdsAdded = [.. diff.UtensilIdsAdded];
        variant.UtensilIdsRemoved = [.. diff.UtensilIdsRemoved];
        variant.PrepMinutes = diff.PrepMinutes;
        variant.CookMinutes = diff.CookMinutes;
        variant.ServingsOverride = diff.ServingsOverride;
    }

    private async Task<VariantDiff> DiffAsync(Recipe recipe, VariantRecipeInput input, Guid userId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var code = ErrorCodes.ValidationFailed;

        // Products and base amounts (PROJECT §4.2).
        var productIds = input.Ingredients.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var units = await db.LoadCustomUnitsAsync(input.Ingredients.Select(i => i.Quantity), ct);
        var ingredients = new List<IngredientDraft>(input.Ingredients.Count);
        for (var i = 0; i < input.Ingredients.Count; i++)
        {
            var row = input.Ingredients[i];
            var path = $"recipe.ingredients[{i}]";
            var note = NormalizeText(row.Note);
            if (note is { Length: > MaxNoteLength })
            {
                errors[$"{path}.note"] = [ErrorCodes.ValidationFailed];
            }

            if (!products.TryGetValue(row.ProductId, out var product))
            {
                errors[$"{path}.productId"] = [ErrorCodes.ProductNotFound];
                continue;
            }

            decimal? baseAmount = null;
            if (row.Quantity is { } quantity)
            {
                if (!quantity.IsWellFormed || (quantity.Size is not null && !product.HasSizes))
                {
                    errors[$"{path}.quantity"] = [ErrorCodes.QuantityInvalid];
                    code = code == ErrorCodes.ValidationFailed ? ErrorCodes.QuantityInvalid : code;
                    continue;
                }

                if (quantity.Unit == Unit.Custom && units.UnitOf(quantity) is null)
                {
                    errors[$"{path}.quantity"] = [ErrorCodes.UnitNotFound];
                    continue;
                }

                baseAmount = converter.ToBaseAmount(quantity, product, units.UnitOf(quantity));
                if (baseAmount is null)
                {
                    errors[$"{path}.quantity"] = [ErrorCodes.QuantityUnitIncompatible];
                    code = ErrorCodes.QuantityUnitIncompatible;
                    continue;
                }
            }

            ingredients.Add(new IngredientDraft(row.Id, row.ProductId, row.Quantity, baseAmount, note, row.Optional));
        }

        // Utensils must exist in the shared catalog.
        var utensilIds = input.UtensilIds.Distinct().ToList();
        var knownUtensils = await db.Utensils.Where(u => utensilIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        for (var i = 0; i < input.UtensilIds.Count; i++)
        {
            if (!knownUtensils.Contains(input.UtensilIds[i]))
            {
                errors[$"recipe.utensilIds[{i}]"] = [ErrorCodes.ValidationFailed];
            }
        }

        ValidateBlocks(input.PrepSteps, "recipe.prepSteps", errors);
        ValidateBlocks(input.InstructionSteps, "recipe.instructionSteps", errors);
        await ValidateImagesAsync(recipe, input, userId, errors, ct);

        if (errors.Count > 0)
        {
            if (errors.Values.Any(v => v.Contains(ErrorCodes.ImageNotOwned) || v.Contains(ErrorCodes.ImageNotFound)) && code == ErrorCodes.ValidationFailed)
            {
                code = errors.Values.Any(v => v.Contains(ErrorCodes.ImageNotFound)) ? ErrorCodes.ImageNotFound : ErrorCodes.ImageNotOwned;
            }

            throw new DomainValidationException(code, errors);
        }

        var draft = new RecipeDraft(ingredients, input.PrepSteps, input.InstructionSteps, input.UtensilIds, input.PrepMinutes, input.CookMinutes, input.Servings);
        return engine.Diff(recipe, draft);
    }

    private static void ValidateBlocks(IReadOnlyList<StepDraft> steps, string path, Dictionary<string, string[]> errors)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Blocks.Count == 0)
            {
                errors[$"{path}[{i}].blocks"] = [ErrorCodes.ValidationFailed];
            }

            for (var b = 0; b < steps[i].Blocks.Count; b++)
            {
                var invalid = steps[i].Blocks[b] switch
                {
                    TextBlock t => string.IsNullOrWhiteSpace(t.Text) || t.Text.Length > MaxTextLength,
                    ImageBlock img => img.Caption is { Length: > MaxCaptionLength },
                    _ => true,
                };
                if (invalid)
                {
                    errors[$"{path}[{i}].blocks[{b}]"] = [ErrorCodes.ValidationFailed];
                }
            }
        }
    }

    // A photo in a variant step must exist and be the caller's own, or already appear in the base recipe or in a
    // variant of it the caller can see ("create a variant from the current selection" copies other variants' steps).
    private async Task ValidateImagesAsync(Recipe recipe, VariantRecipeInput input, Guid userId, Dictionary<string, string[]> errors, CancellationToken ct)
    {
        var referenced = new List<(string Path, Guid ImageId)>();
        Collect(input.PrepSteps, "recipe.prepSteps");
        Collect(input.InstructionSteps, "recipe.instructionSteps");
        if (referenced.Count == 0)
        {
            return;
        }

        var allowed = recipe.Steps.SelectMany(s => s.Blocks).OfType<ImageBlock>().Select(b => b.ImageId).ToHashSet();
        var candidates = referenced.Select(r => r.ImageId).Where(id => !allowed.Contains(id)).Distinct().ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var images = await db.Images.Where(i => candidates.Contains(i.Id)).Select(i => new { i.Id, i.OwnerId }).ToDictionaryAsync(i => i.Id, i => i.OwnerId, ct);
        if (candidates.Any(id => images.TryGetValue(id, out var owner) && owner != userId))
        {
            var foreignOps = await visibility.VisibleVariants(userId).Where(v => v.RecipeId == recipe.Id).Select(v => v.StepOps).ToListAsync(ct);
            foreach (var op in foreignOps.SelectMany(ops => ops))
            {
                var blocks = op switch
                {
                    ReplaceStepOp r => r.Blocks,
                    AddStepOp a => a.Blocks,
                    _ => [],
                };
                allowed.UnionWith(blocks.OfType<ImageBlock>().Select(b => b.ImageId));
            }
        }

        foreach (var (path, imageId) in referenced.Where(r => !allowed.Contains(r.ImageId)))
        {
            if (!images.TryGetValue(imageId, out var owner))
            {
                errors[path] = [ErrorCodes.ImageNotFound];
            }
            else if (owner != userId)
            {
                errors[path] = [ErrorCodes.ImageNotOwned];
            }
        }

        void Collect(IReadOnlyList<StepDraft> steps, string path)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                for (var b = 0; b < steps[i].Blocks.Count; b++)
                {
                    if (steps[i].Blocks[b] is ImageBlock image)
                    {
                        referenced.Add(($"{path}[{i}].blocks[{b}].imageId", image.ImageId));
                    }
                }
            }
        }
    }

    private static string NormalizeName(string name)
    {
        var trimmed = (name ?? "").Trim();
        return trimmed.Length is >= 1 and <= 80
            ? trimmed
            : throw new DomainValidationException(ErrorCodes.ValidationFailed, new Dictionary<string, string[]> { ["name"] = [ErrorCodes.ValidationFailed] });
    }

    private static string? NormalizeText(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
