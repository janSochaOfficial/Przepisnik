using System.Transactions;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Fridges;

/// <summary>A recorded deduction and whether it also counted as a cook (false without a recipe, or inside the 12 hours, D-43).</summary>
public sealed record DeductionOutcome(Deduction Deduction, bool CookCounted);

/// <summary>Deducting a cooked recipe from a fridge, and undoing it (API.md §7, PROJECT §9, D-10).</summary>
public interface IDeductionService
{
    /// <summary>
    /// Deducts every line from the product's packages (most-used first, <see cref="DeductionAllocator"/>), records the
    /// previous state of each touched item and counts the cook through <see cref="ICookService.TryCountAsync"/> (at most
    /// once per user and recipe per 12 hours; the deduction itself is never limited).
    /// </summary>
    Task<DeductionOutcome> DeductAsync(Guid fridgeId, DeductionInput input, CancellationToken ct);

    /// <summary>
    /// Restores every touched item from its snapshot (re-creating removed ones under the same id). 409
    /// <c>deduction.items_changed</c> when any of them changed since, 409 <c>deduction.already_undone</c> the second
    /// time. Cook counts are not decremented.
    /// </summary>
    Task<Deduction> UndoAsync(Guid fridgeId, Guid deductionId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class DeductionService(
    IAppDbContext db,
    ICurrentUser currentUser,
    FridgeAccess access,
    VisibilityRules visibility,
    ICookService cooks,
    TimeProvider time) : IDeductionService
{
    /// <inheritdoc />
    public async Task<DeductionOutcome> DeductAsync(Guid fridgeId, DeductionInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var me = currentUser.Id;
        await access.RequireMemberAsync(fridgeId, ct);
        Validate(input);

        // Existence checks only: the recipe and variant rows are never loaded or saved here (D-28).
        if (input.RecipeId is { } recipeId
            && !await visibility.VisibleRecipes(me).AnyAsync(r => r.Id == recipeId, ct))
        {
            throw new NotFoundException(ErrorCodes.RecipeNotFound);
        }

        if (input.VariantId is { } variantId
            && !await visibility.VisibleVariants(me).AnyAsync(v => v.Id == variantId && v.RecipeId == input.RecipeId, ct))
        {
            throw new NotFoundException(ErrorCodes.VariantNotFound);
        }

        var productIds = input.Lines.Select(l => l.ProductId).ToList();
        var items = await db.FridgeItems
            .Where(i => i.FridgeId == fridgeId && productIds.Contains(i.ProductId))
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            throw new DomainValidationException(ErrorCodes.DeductionEmpty, message: "None of the products is in this fridge.");
        }

        var byId = items.ToDictionary(i => i.Id);
        var now = time.GetUtcNow();
        var planned = new List<(DeductionLineInput Line, AllocationResult Result, Dictionary<Guid, FridgeItemSnapshot> Before)>();
        foreach (var line in input.Lines)
        {
            var packages = items.Where(i => i.ProductId == line.ProductId).ToList();
            var result = DeductionAllocator.Allocate(
                packages.Select(i => new AllocatorItem(i.Id, i.PackageAmount, i.RemainingPercent)), line.Amount);
            var before = new Dictionary<Guid, FridgeItemSnapshot>();
            foreach (var change in result.Items)
            {
                var item = byId[change.ItemId];
                before[item.Id] = Snapshot(item);
                if (change.Removed)
                {
                    db.FridgeItems.Remove(item);
                }
                else
                {
                    item.RemainingPercent = change.NewPercent;
                    item.UpdatedAt = now;
                }
            }

            planned.Add((line, result, before));
        }

        // Two saves in one local transaction: the deduction records the items' xmin, which exists only after the first.
        // The cook count joins it, so it runs at the cook's isolation level.
        using var scope = new TransactionScope(TransactionScopeOption.Required, ICookService.ScopeOptions, TransactionScopeAsyncFlowOption.Enabled);
        await db.SaveChangesAsync(ct);

        // One atomic statement decides whether the cook counts (12-hour rule), in the same transaction; the recipe's and
        // variant's xmin is untouched.
        var cookCounted = input.RecipeId is { } cookedRecipe && (await cooks.TryCountAsync(me, cookedRecipe, input.VariantId, ct)).Counted;

        var deduction = new Deduction
        {
            FridgeId = fridgeId,
            UserId = me,
            RecipeId = input.RecipeId,
            VariantId = input.VariantId,
            CreatedAt = now,
            Lines = [.. planned.Select(p => new DeductionLine(
                p.Line.ProductId,
                p.Result.Requested,
                p.Result.Deducted,
                p.Result.Shortfall,
                [.. p.Result.Items.Select(c => new DeductionItemChange(
                    c.ItemId,
                    c.PreviousPercent,
                    c.NewPercent,
                    c.Removed,
                    c.Removed ? null : byId[c.ItemId].Version,
                    p.Before[c.ItemId]))]))],
        };
        db.Deductions.Add(deduction);
        await db.SaveChangesAsync(ct);
        scope.Complete();
        return new DeductionOutcome(deduction, cookCounted);
    }

    /// <inheritdoc />
    public async Task<Deduction> UndoAsync(Guid fridgeId, Guid deductionId, CancellationToken ct)
    {
        await access.RequireMemberAsync(fridgeId, ct);
        var deduction = await db.Deductions.SingleOrDefaultAsync(d => d.Id == deductionId && d.FridgeId == fridgeId, ct)
            ?? throw new NotFoundException(ErrorCodes.DeductionNotFound);
        if (deduction.UndoneAt is not null)
        {
            throw new ConflictException(ErrorCodes.DeductionAlreadyUndone);
        }

        var changes = deduction.Lines.SelectMany(l => l.Items).ToList();
        var ids = changes.Select(c => c.ItemId).ToList();
        var current = await db.FridgeItems.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        var unchanged = changes.All(c => c.Removed
            ? !current.ContainsKey(c.ItemId)
            : current.TryGetValue(c.ItemId, out var item) && item.FridgeId == fridgeId && item.Version == c.VersionAfter);
        if (!unchanged)
        {
            throw new ConflictException(ErrorCodes.DeductionItemsChanged, "An item touched by the deduction changed since.");
        }

        foreach (var change in changes)
        {
            var before = change.Before;
            if (change.Removed)
            {
                db.FridgeItems.Add(new FridgeItem
                {
                    Id = change.ItemId,
                    FridgeId = fridgeId,
                    ProductId = before.ProductId,
                    PackageAmount = before.PackageAmount,
                    RemainingPercent = before.RemainingPercent,
                    Note = before.Note,
                    AddedById = before.AddedById,
                    CreatedAt = before.CreatedAt,
                    UpdatedAt = before.UpdatedAt,
                });
            }
            else
            {
                var item = current[change.ItemId];
                item.ProductId = before.ProductId;
                item.PackageAmount = before.PackageAmount;
                item.RemainingPercent = before.RemainingPercent;
                item.Note = before.Note;
                item.UpdatedAt = before.UpdatedAt;
            }
        }

        deduction.UndoneAt = time.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A simultaneous undo (or edit) got there first: the re-inserted id or an item's version clashed.
            var undone = await db.Deductions.AsNoTracking().AnyAsync(d => d.Id == deductionId && d.UndoneAt != null, ct);
            throw new ConflictException(undone ? ErrorCodes.DeductionAlreadyUndone : ErrorCodes.DeductionItemsChanged);
        }

        return deduction;
    }

    private static void Validate(DeductionInput input)
    {
        if (input.Lines is null || input.Lines.Count == 0)
        {
            throw new DomainValidationException(ErrorCodes.DeductionEmpty, message: "A deduction needs at least one line.");
        }

        var errors = new ValidationErrors();
        if (input.VariantId is not null && input.RecipeId is null)
        {
            errors.Add("recipeId", "required");
        }

        var seen = new HashSet<Guid>();
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            if (line.Amount <= 0)
            {
                errors.Add($"lines[{i}].amount", "out_of_range");
            }

            if (!seen.Add(line.ProductId))
            {
                errors.Add($"lines[{i}].productId", "duplicate");
            }
        }

        errors.ThrowIfAny();
    }

    private static FridgeItemSnapshot Snapshot(FridgeItem item) => new(
        item.ProductId, item.PackageAmount, item.RemainingPercent, item.Note, item.AddedById, item.CreatedAt, item.UpdatedAt);
}
