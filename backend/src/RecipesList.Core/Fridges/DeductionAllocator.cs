namespace RecipesList.Core.Fridges;

/// <summary>One package of the product being deducted, as the allocator sees it.</summary>
public sealed record AllocatorItem(Guid ItemId, decimal PackageAmount, decimal RemainingPercent);

/// <summary>What the allocator does to one package. <c>Removed</c> is true when the package is used up (new percent 0).</summary>
public sealed record ItemAllocation(Guid ItemId, decimal PreviousPercent, decimal NewPercent, bool Removed);

/// <summary>
/// The outcome of deducting one product: the touched packages in consumption order, how much was deducted and how much
/// was missing (<c>Deducted + Shortfall == Requested</c>).
/// </summary>
public sealed record AllocationResult(decimal Requested, decimal Deducted, decimal Shortfall, IReadOnlyList<ItemAllocation> Items);

/// <summary>
/// Deducts an amount of one product from its packages in a fridge (PROJECT §9, D-10). Pure: no I/O, no clock.
/// </summary>
public static class DeductionAllocator
{
    /// <summary>Remaining percents are stored with 2 decimals (API.md §6 <c>FridgeItem.remainingPercent</c>).</summary>
    public const int PercentDecimals = 2;

    /// <summary>
    /// Consumes <paramref name="amount"/> (base units, &gt; 0) from <paramref name="items"/>, the most-used package first
    /// (lowest remaining percent; ties go to the smaller remaining amount, then to the order given). A package that is
    /// emptied, or whose rounded remainder is 0 %, is removed. New percents are rounded to 2 decimals and never negative.
    /// Whatever cannot be covered is the shortfall.
    /// </summary>
    public static AllocationResult Allocate(IEnumerable<AllocatorItem> items, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var ordered = items
            .Select((item, index) => (item, index))
            .Where(x => x.item.PackageAmount > 0 && x.item.RemainingPercent > 0)
            .OrderBy(x => x.item.RemainingPercent)
            .ThenBy(x => Available(x.item))
            .ThenBy(x => x.index)
            .Select(x => x.item);

        var need = amount;
        var changes = new List<ItemAllocation>();
        foreach (var item in ordered)
        {
            if (need <= 0)
            {
                break;
            }

            var available = Available(item);
            if (need >= available)
            {
                need -= available;
                changes.Add(new ItemAllocation(item.ItemId, item.RemainingPercent, 0m, Removed: true));
                continue;
            }

            var newPercent = Math.Round((available - need) / item.PackageAmount * 100m, PercentDecimals, MidpointRounding.AwayFromZero);
            need = 0;
            newPercent = Math.Clamp(newPercent, 0m, item.RemainingPercent);
            changes.Add(new ItemAllocation(item.ItemId, item.RemainingPercent, newPercent, Removed: newPercent == 0m));
        }

        var shortfall = Math.Max(need, 0m);
        return new AllocationResult(amount, amount - shortfall, shortfall, changes);
    }

    private static decimal Available(AllocatorItem item) => item.PackageAmount * item.RemainingPercent / 100m;
}
