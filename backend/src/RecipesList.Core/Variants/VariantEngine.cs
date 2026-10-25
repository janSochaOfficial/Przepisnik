using RecipesList.Core.Common;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Variants;

/// <summary>
/// Diff, resolve and matrix logic for variants (PROJECT §6, API.md §5, D-12, D-20). Pure: no I/O, no clock.
/// </summary>
/// <remarks>
/// <para><b>Diff rules.</b> A row whose id is a base row id and whose content changed becomes <c>modify</c> (ingredients)
/// or <c>replace</c> (steps); a base row missing from the edit becomes <c>remove</c>; any other row (no id, or an id that
/// is not a base row id) becomes <c>add</c> with <c>afterId</c> = the previous row in the edited order (base or added),
/// or null at the start. Operations are emitted as: modify/replace and remove in base order, then adds in edited order,
/// so every <c>afterId</c> refers to a row that already exists when the op is applied.</para>
/// <para><b>Not representable in v1</b> (rejected with 400 <c>validation.failed</c> and a field path): reordering base
/// rows (D-20, the variant editor locks their order), a base row sent twice or in the other step section, and clearing
/// a time the base sets (a null override means "inherit").</para>
/// <para><b>Servings</b> (PROJECT §5.7): an override only when the edited value differs from the base; an edited copy
/// without servings inherits the base's (unlike a time, clearing is not an error: R2 "cleared → inherit").</para>
/// </remarks>
public sealed class VariantEngine(Func<Guid>? newId = null) : IVariantEngine
{
    private const decimal SameOptionTolerance = 0.01m;

    private readonly Func<Guid> _newId = newId ?? Guid.CreateVersion7;

    /// <inheritdoc />
    public VariantDiff Diff(Recipe baseRecipe, RecipeDraft edited)
    {
        ArgumentNullException.ThrowIfNull(baseRecipe);
        ArgumentNullException.ThrowIfNull(edited);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var ingredientOps = DiffIngredients(BaseIngredients(baseRecipe), edited.Ingredients, errors);

        var prep = BaseSteps(baseRecipe, StepSection.Prep);
        var instructions = BaseSteps(baseRecipe, StepSection.Instructions);
        var stepOps = new List<StepOp>();
        stepOps.AddRange(DiffSteps(StepSection.Prep, prep, edited.PrepSteps, instructions, "recipe.prepSteps", errors));
        stepOps.AddRange(DiffSteps(StepSection.Instructions, instructions, edited.InstructionSteps, prep, "recipe.instructionSteps", errors));

        var baseUtensils = baseRecipe.Utensils.Select(u => u.UtensilId).ToList();
        var editedUtensils = edited.UtensilIds.Distinct().ToList();
        var added = editedUtensils.Where(id => !baseUtensils.Contains(id)).ToList();
        var removed = baseUtensils.Where(id => !editedUtensils.Contains(id)).Distinct().ToList();

        var prepOverride = TimeOverride(baseRecipe.PrepMinutes, edited.PrepMinutes, "recipe.prepMinutes", errors);
        var cookOverride = TimeOverride(baseRecipe.CookMinutes, edited.CookMinutes, "recipe.cookMinutes", errors);
        var servingsOverride = edited.Servings == baseRecipe.Servings ? null : edited.Servings;

        if (errors.Count > 0)
        {
            throw new DomainValidationException(ErrorCodes.ValidationFailed, errors, "The edited copy cannot be expressed as variant operations.");
        }

        return new VariantDiff(ingredientOps, stepOps, added, removed, prepOverride, cookOverride, servingsOverride);
    }

    /// <inheritdoc />
    public ResolvedRecipeModel Resolve(Recipe baseRecipe, Variant? variant)
    {
        ArgumentNullException.ThrowIfNull(baseRecipe);

        var utensils = baseRecipe.Utensils.Select(u => u.UtensilId).Distinct().ToList();
        if (variant is not null)
        {
            utensils.RemoveAll(variant.UtensilIdsRemoved.Contains);
            utensils.AddRange(variant.UtensilIdsAdded.Where(id => !utensils.Contains(id)).Distinct());
        }

        return new ResolvedRecipeModel(
            baseRecipe.Id,
            variant?.Id,
            ResolveIngredients(baseRecipe, variant),
            ResolveSteps(baseRecipe, variant, StepSection.Prep),
            ResolveSteps(baseRecipe, variant, StepSection.Instructions),
            utensils,
            variant?.PrepMinutes ?? baseRecipe.PrepMinutes,
            variant?.CookMinutes ?? baseRecipe.CookMinutes,
            variant?.ServingsOverride ?? baseRecipe.Servings);
    }

    /// <inheritdoc />
    /// <remarks>Only ingredients are resolved, so <paramref name="baseRecipe"/> needs its ingredients loaded, not its steps.</remarks>
    public VariantMatrixModel BuildMatrix(Recipe baseRecipe, IReadOnlyList<Variant> variants)
    {
        ArgumentNullException.ThrowIfNull(baseRecipe);
        ArgumentNullException.ThrowIfNull(variants);

        var baseRows = BaseIngredients(baseRecipe);
        var baseIds = baseRows.Select(r => r.Id).ToHashSet();

        // Column 0 is the base (variant id null); then the variants in the given order.
        var columns = new List<(Guid? VariantId, IReadOnlyList<ResolvedIngredient> Ingredients)>(variants.Count + 1)
        {
            (null, baseRows),
        };
        columns.AddRange(variants.Select(v => ((Guid?)v.Id, (IReadOnlyList<ResolvedIngredient>)ResolveIngredients(baseRecipe, v))));

        var slots = new List<SlotModel>();
        foreach (var row in baseRows)
        {
            slots.Add(BuildSlot(
                MatrixKeys.BaseSlot(row.Id),
                row.ProductId,
                columns.Select(c => (c.VariantId, c.Ingredients.FirstOrDefault(i => i.Id == row.Id)))));
        }

        // Added rows are grouped into one slot per product, in first-seen order over the columns. A variant that adds
        // the same product twice is represented by its first such row (a slot holds one option per variant).
        var addedProducts = columns
            .SelectMany(c => c.Ingredients.Where(i => !baseIds.Contains(i.Id)).Select(i => i.ProductId))
            .Distinct()
            .ToList();
        foreach (var productId in addedProducts)
        {
            var slot = BuildSlot(
                MatrixKeys.AddedSlot(productId),
                productId,
                columns.Select(c => (c.VariantId, c.Ingredients.FirstOrDefault(i => !baseIds.Contains(i.Id) && i.ProductId == productId))));
            slots.Add(slot with { AfterSlotKey = AfterSlotKey(columns.Select(c => c.Ingredients), baseIds, productId) });
        }

        var matrixVariants = columns
            .Select(c => new MatrixVariantModel(c.VariantId, c.VariantId is null ? [] : ProductSwaps(baseRows, c.Ingredients)))
            .ToList();

        return new VariantMatrixModel(baseRecipe.Id, matrixVariants, slots);
    }

    /// <summary>
    /// Whether two base amounts of the same product are one option: both null ("do smaku"), or within 1 % of the
    /// option's first-seen amount <paramref name="optionAmount"/>.
    /// </summary>
    public static bool IsSameAmount(decimal? optionAmount, decimal? amount) =>
        (optionAmount, amount) switch
        {
            (null, null) => true,
            ({ } k, { } a) => Math.Abs(a - k) <= Math.Abs(k) * SameOptionTolerance,
            _ => false,
        };

    private static SlotModel BuildSlot(string key, Guid productId, IEnumerable<(Guid? VariantId, ResolvedIngredient? Ingredient)> cells)
    {
        var options = new List<(string Key, ResolvedIngredient? Ingredient, List<Guid?> VariantIds)>();
        foreach (var (variantId, ingredient) in cells)
        {
            var index = options.FindIndex(o => Matches(o.Key, o.Ingredient, ingredient));
            if (index < 0)
            {
                var optionKey = ingredient is null ? MatrixKeys.None : MatrixKeys.Option(ingredient.ProductId, ingredient.BaseAmount);
                options.Add((optionKey, ingredient, [variantId]));
            }
            else
            {
                options[index].VariantIds.Add(variantId);
            }
        }

        return new SlotModel(key, productId, [.. options.Select(o => new SlotOptionModel(o.Key, o.Ingredient, o.VariantIds))]);
    }

    private static bool Matches(string optionKey, ResolvedIngredient? option, ResolvedIngredient? candidate)
    {
        if (option is null || candidate is null)
        {
            return option is null && candidate is null;
        }

        return option.ProductId == candidate.ProductId
            && (IsSameAmount(option.BaseAmount, candidate.BaseAmount)
                // Keys must stay unique: amounts that round to the same key are one option.
                || optionKey == MatrixKeys.Option(candidate.ProductId, candidate.BaseAmount));
    }

    // The position hint of an added slot: the nearest base row before the first-seen added row of the product.
    private static string? AfterSlotKey(IEnumerable<IReadOnlyList<ResolvedIngredient>> columns, HashSet<Guid> baseIds, Guid productId)
    {
        foreach (var rows in columns)
        {
            var index = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (!baseIds.Contains(rows[i].Id) && rows[i].ProductId == productId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                continue;
            }

            for (var i = index - 1; i >= 0; i--)
            {
                if (baseIds.Contains(rows[i].Id))
                {
                    return MatrixKeys.BaseSlot(rows[i].Id);
                }
            }

            return null;
        }

        return null;
    }

    private static List<ProductSwap> ProductSwaps(IReadOnlyList<ResolvedIngredient> baseRows, IReadOnlyList<ResolvedIngredient> resolved)
    {
        var swaps = new List<ProductSwap>();
        foreach (var row in baseRows)
        {
            var current = resolved.FirstOrDefault(i => i.Id == row.Id);
            if (current is not null && current.ProductId != row.ProductId)
            {
                var swap = new ProductSwap(row.ProductId, current.ProductId);
                if (!swaps.Contains(swap))
                {
                    swaps.Add(swap);
                }
            }
        }

        return swaps;
    }

    private static List<ResolvedIngredient> BaseIngredients(Recipe recipe) =>
        [.. recipe.Ingredients
            .OrderBy(i => i.Position)
            .Select(i => new ResolvedIngredient(i.Id, i.ProductId, i.Quantity, i.BaseAmount, i.Note, i.Optional))];

    private static List<ResolvedStep> BaseSteps(Recipe recipe, StepSection section) =>
        [.. recipe.Steps
            .Where(s => s.Section == section)
            .OrderBy(s => s.Position)
            .Select(s => new ResolvedStep(s.Id, s.Blocks))];

    private static List<ResolvedIngredient> ResolveIngredients(Recipe baseRecipe, Variant? variant)
    {
        var rows = BaseIngredients(baseRecipe);
        if (variant is null)
        {
            return rows;
        }

        var baseIds = rows.Select(r => r.Id).ToHashSet();
        for (var n = 0; n < variant.IngredientOps.Count; n++)
        {
            switch (variant.IngredientOps[n])
            {
                case ModifyIngredientOp op:
                    {
                        var index = IndexOfBaseRow(rows, baseIds, op.TargetId, variant, n, "modify");
                        rows[index] = new ResolvedIngredient(op.TargetId, op.ProductId, op.Quantity, op.BaseAmount, op.Note, op.Optional);
                        break;
                    }

                case RemoveIngredientOp op:
                    rows.RemoveAt(IndexOfBaseRow(rows, baseIds, op.TargetId, variant, n, "remove"));
                    break;

                case AddIngredientOp op:
                    {
                        var at = InsertionIndex(rows, r => r.Id, op.Id, op.AfterId, variant, $"ingredient op #{n} (add)");
                        rows.Insert(at, new ResolvedIngredient(op.Id, op.ProductId, op.Quantity, op.BaseAmount, op.Note, op.Optional));
                        break;
                    }

                default:
                    throw new InvalidOperationException($"Variant {variant.Id}: unknown ingredient op #{n} ({variant.IngredientOps[n].GetType().Name}).");
            }
        }

        return rows;
    }

    private static List<ResolvedStep> ResolveSteps(Recipe baseRecipe, Variant? variant, StepSection section)
    {
        var steps = BaseSteps(baseRecipe, section);
        if (variant is null)
        {
            return steps;
        }

        var baseIds = steps.Select(s => s.Id).ToHashSet();
        for (var n = 0; n < variant.StepOps.Count; n++)
        {
            var stepOp = variant.StepOps[n];
            if (stepOp.Section != section)
            {
                continue;
            }

            switch (stepOp)
            {
                case ReplaceStepOp op:
                    {
                        var index = IndexOfBaseStep(steps, baseIds, op.TargetId, variant, n, "replace");
                        steps[index] = new ResolvedStep(op.TargetId, op.Blocks);
                        break;
                    }

                case RemoveStepOp op:
                    steps.RemoveAt(IndexOfBaseStep(steps, baseIds, op.TargetId, variant, n, "remove"));
                    break;

                case AddStepOp op:
                    {
                        var at = InsertionIndex(steps, s => s.Id, op.Id, op.AfterId, variant, $"step op #{n} (add, {section})");
                        steps.Insert(at, new ResolvedStep(op.Id, op.Blocks));
                        break;
                    }

                default:
                    throw new InvalidOperationException($"Variant {variant.Id}: unknown step op #{n} ({stepOp.GetType().Name}).");
            }
        }

        return steps;
    }

    // A base recipe never changes while it has variants (PROJECT §6.4), so a missing target means corrupted data.
    private static int IndexOfBaseRow(List<ResolvedIngredient> rows, HashSet<Guid> baseIds, Guid targetId, Variant variant, int n, string kind)
    {
        var index = baseIds.Contains(targetId) ? rows.FindIndex(r => r.Id == targetId) : -1;
        return index >= 0
            ? index
            : throw new InvalidOperationException(
                $"Variant {variant.Id}: ingredient op #{n} ({kind}) targets ingredient {targetId}, which is not a remaining base row of recipe {variant.RecipeId}.");
    }

    private static int IndexOfBaseStep(List<ResolvedStep> steps, HashSet<Guid> baseIds, Guid targetId, Variant variant, int n, string kind)
    {
        var index = baseIds.Contains(targetId) ? steps.FindIndex(s => s.Id == targetId) : -1;
        return index >= 0
            ? index
            : throw new InvalidOperationException(
                $"Variant {variant.Id}: step op #{n} ({kind}) targets step {targetId}, which is not a remaining base step of recipe {variant.RecipeId}.");
    }

    private static int InsertionIndex<T>(List<T> rows, Func<T, Guid> id, Guid newId, Guid? afterId, Variant variant, string opName)
    {
        if (rows.Exists(r => id(r) == newId))
        {
            throw new InvalidOperationException($"Variant {variant.Id}: {opName} adds row {newId}, which already exists.");
        }

        if (afterId is not { } after)
        {
            return 0;
        }

        var index = rows.FindIndex(r => id(r) == after);
        return index >= 0
            ? index + 1
            : throw new InvalidOperationException($"Variant {variant.Id}: {opName} inserts after {after}, which does not exist.");
    }

    private List<IngredientOp> DiffIngredients(
        List<ResolvedIngredient> baseRows,
        IReadOnlyList<IngredientDraft> drafts,
        Dictionary<string, string[]> errors)
    {
        var rows = DiffRows(
            baseRows.Select(r => r.Id).ToList(),
            drafts.Select(d => d.Id).ToList(),
            reservedIds: [],
            "recipe.ingredients",
            errors);

        var ops = new List<IngredientOp>();
        foreach (var (baseIndex, draftIndex) in rows.Kept)
        {
            var b = baseRows[baseIndex];
            var d = drafts[draftIndex];
            if (b.ProductId != d.ProductId || !Equals(b.Quantity, d.Quantity) || NormalizeNote(b.Note) != NormalizeNote(d.Note) || b.Optional != d.Optional)
            {
                ops.Add(new ModifyIngredientOp(b.Id, d.ProductId, d.Quantity, d.BaseAmount, NormalizeNote(d.Note), d.Optional));
            }
        }

        ops.AddRange(rows.Removed.Select(i => new RemoveIngredientOp(baseRows[i].Id)));
        SortByBaseOrder(ops, baseRows.Select(r => r.Id).ToList(), op => op switch
        {
            ModifyIngredientOp m => m.TargetId,
            RemoveIngredientOp r => r.TargetId,
            _ => Guid.Empty,
        });

        foreach (var (draftIndex, id, afterId) in rows.Added)
        {
            var d = drafts[draftIndex];
            ops.Add(new AddIngredientOp(id, afterId, d.ProductId, d.Quantity, d.BaseAmount, NormalizeNote(d.Note), d.Optional));
        }

        return ops;
    }

    private List<StepOp> DiffSteps(
        StepSection section,
        List<ResolvedStep> baseSteps,
        IReadOnlyList<StepDraft> drafts,
        List<ResolvedStep> otherSection,
        string path,
        Dictionary<string, string[]> errors)
    {
        var rows = DiffRows(
            baseSteps.Select(s => s.Id).ToList(),
            drafts.Select(d => d.Id).ToList(),
            otherSection.Select(s => s.Id).ToHashSet(),
            path,
            errors);

        var ops = new List<StepOp>();
        foreach (var (baseIndex, draftIndex) in rows.Kept)
        {
            if (!baseSteps[baseIndex].Blocks.SequenceEqual(drafts[draftIndex].Blocks))
            {
                ops.Add(new ReplaceStepOp(section, baseSteps[baseIndex].Id, [.. drafts[draftIndex].Blocks]));
            }
        }

        ops.AddRange(rows.Removed.Select(i => new RemoveStepOp(section, baseSteps[i].Id)));
        SortByBaseOrder(ops, baseSteps.Select(s => s.Id).ToList(), op => op switch
        {
            ReplaceStepOp r => r.TargetId,
            RemoveStepOp r => r.TargetId,
            _ => Guid.Empty,
        });

        ops.AddRange(rows.Added.Select(a => new AddStepOp(section, a.Id, a.AfterId, [.. drafts[a.DraftIndex].Blocks])));
        return ops;
    }

    private static void SortByBaseOrder<T>(List<T> ops, List<Guid> baseOrder, Func<T, Guid> target)
    {
        var position = baseOrder.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var sorted = ops.OrderBy(op => position[target(op)]).ToList();
        ops.Clear();
        ops.AddRange(sorted);
    }

    private RowDiff DiffRows(
        List<Guid> baseIds,
        List<Guid?> draftIds,
        HashSet<Guid> reservedIds,
        string path,
        Dictionary<string, string[]> errors)
    {
        var baseIndex = baseIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var kept = new List<(int BaseIndex, int DraftIndex)>();
        var added = new List<(int DraftIndex, Guid Id, Guid? AfterId)>();
        var seen = new HashSet<Guid>();
        var usedAddIds = new HashSet<Guid>();
        var lastBaseIndex = -1;
        Guid? previous = null;

        for (var i = 0; i < draftIds.Count; i++)
        {
            var id = draftIds[i];
            if (id is { } existing && baseIndex.TryGetValue(existing, out var index))
            {
                if (!seen.Add(existing))
                {
                    errors[$"{path}[{i}].id"] = [ErrorCodes.ValidationFailed];
                    continue;
                }

                if (index < lastBaseIndex)
                {
                    // D-20: base rows cannot be reordered by a variant.
                    errors[$"{path}[{i}].id"] = [ErrorCodes.ValidationFailed];
                }

                lastBaseIndex = Math.Max(lastBaseIndex, index);
                kept.Add((index, i));
                previous = existing;
                continue;
            }

            if (id is { } misplaced && reservedIds.Contains(misplaced))
            {
                // A base step moved to the other section.
                errors[$"{path}[{i}].id"] = [ErrorCodes.ValidationFailed];
                continue;
            }

            // A row the base does not have: keep the client's id when it is usable, else mint one.
            var newId = id is { } candidate && candidate != Guid.Empty && !usedAddIds.Contains(candidate) ? candidate : _newId();
            usedAddIds.Add(newId);
            added.Add((i, newId, previous));
            previous = newId;
        }

        var removed = Enumerable.Range(0, baseIds.Count).Where(i => !seen.Contains(baseIds[i])).ToList();
        return new RowDiff(kept, removed, added);
    }

    private static int? TimeOverride(int? baseMinutes, int? editedMinutes, string path, Dictionary<string, string[]> errors)
    {
        if (editedMinutes == baseMinutes)
        {
            return null;
        }

        if (editedMinutes is null)
        {
            // A null override means "inherit", so clearing a time the base sets cannot be expressed.
            errors[path] = [ErrorCodes.ValidationFailed];
            return null;
        }

        return editedMinutes;
    }

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note;

    private sealed record RowDiff(
        List<(int BaseIndex, int DraftIndex)> Kept,
        List<int> Removed,
        List<(int DraftIndex, Guid Id, Guid? AfterId)> Added);
}
