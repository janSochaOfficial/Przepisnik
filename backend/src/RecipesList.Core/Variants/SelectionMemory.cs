using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Variants;

/// <summary>
/// Remembered picks per user and recipe, and the cross-recipe product preferences derived from them
/// (PROJECT §6.6, API.md §5.2).
/// </summary>
public sealed class SelectionMemory(
    IAppDbContext db,
    ICurrentUser currentUser,
    VisibilityRules visibility,
    IVariantEngine engine,
    TimeProvider clock)
{
    /// <summary>The caller's selection for a visible recipe, or null when nothing is remembered yet.</summary>
    public async Task<RecipeSelection?> GetAsync(Guid recipeId, CancellationToken ct)
    {
        var userId = currentUser.Id;
        if (!await visibility.VisibleRecipes(userId).AnyAsync(r => r.Id == recipeId, ct))
        {
            throw new NotFoundException(ErrorCodes.RecipeNotFound);
        }

        return await db.RecipeSelections.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId && s.RecipeId == recipeId, ct);
    }

    /// <summary>
    /// Stores the caller's selection. Malformed slot keys → 400 <c>selection.unknown_slot</c>; well-formed keys of slots
    /// or options that no longer exist in the caller's matrix (e.g. of a deleted variant) are dropped. The caller's
    /// product preferences are recomputed in the same <c>SaveChanges</c>.
    /// </summary>
    public async Task<RecipeSelection> PutAsync(Guid recipeId, SelectionInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var userId = currentUser.Id;

        var malformed = input.Picks.Keys.Where(k => !MatrixKeys.IsWellFormedSlot(k)).Select(k => $"picks.{k}").ToList();
        if (input.Scaling is { } scaling && !MatrixKeys.IsWellFormedSlot(scaling.AnchorSlotKey))
        {
            malformed.Add("scaling.anchorSlotKey");
        }

        if (malformed.Count > 0)
        {
            throw new DomainValidationException(
                ErrorCodes.SelectionUnknownSlot,
                malformed.ToDictionary(path => path, _ => new[] { ErrorCodes.SelectionUnknownSlot }, StringComparer.Ordinal));
        }

        var recipe = await visibility.VisibleRecipes(userId)
            .Where(r => r.Id == recipeId)
            .Include(r => r.Ingredients)
            .AsNoTracking()
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        var variants = await visibility.VisibleVariants(userId)
            .Where(v => v.RecipeId == recipeId)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .AsNoTracking()
            .ToListAsync(ct);
        var options = OptionKeys(engine.BuildMatrix(recipe, variants));

        var selection = await db.RecipeSelections.SingleOrDefaultAsync(s => s.UserId == userId && s.RecipeId == recipeId, ct);
        if (selection is null)
        {
            selection = new RecipeSelection { UserId = userId, RecipeId = recipeId };
            db.RecipeSelections.Add(selection);
        }

        selection.Picks = Keep(input.Picks, options);
        selection.Scaling = input.Scaling is { } s && options.ContainsKey(s.AnchorSlotKey) ? s : null;
        selection.UpdatedAt = clock.GetUtcNow();

        await RecomputePreferencesAsync([userId], [selection], ct);
        await db.SaveChangesAsync(ct);
        return selection;
    }

    /// <summary>
    /// Best effort, without saving: removes remembered picks of <paramref name="recipe"/> (ingredients loaded) whose
    /// slot or option no longer exists among its variants (all of them, except <paramref name="excludeVariantId"/>),
    /// and recomputes the preferences of the affected users. The caller saves everything in one <c>SaveChanges</c>.
    /// </summary>
    public async Task DropStalePicksAsync(Recipe recipe, Guid? excludeVariantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        // Tracked: a variant being updated in this unit of work is seen with its new operations.
        var variants = await db.Variants
            .Where(v => v.RecipeId == recipe.Id && v.Id != excludeVariantId)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .ToListAsync(ct);

        Dictionary<string, HashSet<string>> options;
        try
        {
            options = OptionKeys(engine.BuildMatrix(recipe, variants));
        }
        catch (InvalidOperationException)
        {
            return; // Corrupted ops elsewhere must not block deleting or fixing this variant.
        }

        var changed = new List<RecipeSelection>();
        foreach (var selection in await db.RecipeSelections.Where(s => s.RecipeId == recipe.Id).ToListAsync(ct))
        {
            var kept = Keep(selection.Picks, options);
            var scalingGone = selection.Scaling is { } s && !options.ContainsKey(s.AnchorSlotKey);
            if (kept.Count != selection.Picks.Count || scalingGone)
            {
                selection.Picks = kept;
                selection.Scaling = scalingGone ? null : selection.Scaling;
                changed.Add(selection);
            }
        }

        if (changed.Count > 0)
        {
            await RecomputePreferencesAsync([.. changed.Select(s => s.UserId).Distinct()], changed, ct);
        }
    }

    /// <summary>
    /// Rebuilds the product preferences of <paramref name="userIds"/> from all their selections (API.md §5.2): for every
    /// <c>user</c> pick of a base slot whose product differs from the base row's product, the pair from → to counts once
    /// per recipe. <paramref name="pending"/> are unsaved selections that take precedence over the stored ones.
    /// </summary>
    private async Task RecomputePreferencesAsync(List<Guid> userIds, IReadOnlyList<RecipeSelection> pending, CancellationToken ct)
    {
        var stored = await db.RecipeSelections.Where(s => userIds.Contains(s.UserId)).ToListAsync(ct);
        var all = pending.Concat(stored).DistinctBy(s => (s.UserId, s.RecipeId)).ToList();

        var userPicks = all
            .SelectMany(s => s.Picks
                .Where(p => p.Value.Source == PickSource.User)
                .Select(p => (Selection: s, IngredientId: MatrixKeys.BaseSlotIngredientId(p.Key), To: MatrixKeys.OptionProductId(p.Value.OptionKey))))
            .Where(p => p.IngredientId is not null && p.To is not null)
            .Select(p => (p.Selection, IngredientId: p.IngredientId!.Value, To: p.To!.Value))
            .ToList();

        var ingredientIds = userPicks.Select(p => p.IngredientId).Distinct().ToList();
        var baseRows = await db.RecipeIngredients
            .Where(i => ingredientIds.Contains(i.Id))
            .Select(i => new { i.Id, i.RecipeId, i.ProductId })
            .ToDictionaryAsync(i => i.Id, ct);
        var toIds = userPicks.Select(p => p.To).Distinct().ToList();
        var knownProducts = (await db.Products.Where(p => toIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();

        var weights = userPicks
            .Where(p => baseRows.TryGetValue(p.IngredientId, out var row)
                && row.RecipeId == p.Selection.RecipeId
                && row.ProductId != p.To
                && knownProducts.Contains(p.To))
            .Select(p => (p.Selection.UserId, p.Selection.RecipeId, From: baseRows[p.IngredientId].ProductId, p.To))
            .Distinct()
            .GroupBy(p => (p.UserId, p.From, p.To))
            .ToDictionary(g => g.Key, g => g.Count());

        var existing = await db.ProductPreferences.Where(p => userIds.Contains(p.UserId)).ToListAsync(ct);
        foreach (var preference in existing)
        {
            if (weights.Remove((preference.UserId, preference.FromProductId, preference.ToProductId), out var weight))
            {
                preference.Weight = weight;
            }
            else
            {
                db.ProductPreferences.Remove(preference);
            }
        }

        foreach (var ((userId, from, to), weight) in weights)
        {
            db.ProductPreferences.Add(new ProductPreference { UserId = userId, FromProductId = from, ToProductId = to, Weight = weight });
        }
    }

    private static Dictionary<string, HashSet<string>> OptionKeys(VariantMatrixModel matrix) =>
        matrix.Slots.ToDictionary(s => s.Key, s => s.Options.Select(o => o.Key).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    private static Dictionary<string, SelectionPick> Keep(IReadOnlyDictionary<string, SelectionPick> picks, Dictionary<string, HashSet<string>> options) =>
        picks
            .Where(p => options.TryGetValue(p.Key, out var keys) && keys.Contains(p.Value.OptionKey))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
}
