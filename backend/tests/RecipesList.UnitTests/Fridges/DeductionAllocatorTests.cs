using RecipesList.Core.Fridges;

namespace RecipesList.UnitTests.Fridges;

public sealed class DeductionAllocatorTests
{
    private static readonly Guid A = Guid.CreateVersion7();
    private static readonly Guid B = Guid.CreateVersion7();
    private static readonly Guid C = Guid.CreateVersion7();

    [Fact]
    public void Allocate_OnePackage_LowersItsPercent()
    {
        var result = DeductionAllocator.Allocate([new AllocatorItem(A, 330m, 100m)], 99m);

        result.Deducted.ShouldBe(99m);
        result.Shortfall.ShouldBe(0m);
        result.Items.ShouldBe([new ItemAllocation(A, 100m, 70m, Removed: false)]);
    }

    [Fact]
    public void Allocate_SeveralPackages_ConsumesMostUsedFirst()
    {
        // 1000 ml at 100 %, 1000 ml at 20 % (200 ml), 1000 ml at 50 % (500 ml): 600 ml empties the 20 % one, then
        // takes 400 ml from the 50 % one and leaves the full package alone.
        var items = new[] { new AllocatorItem(A, 1000m, 100m), new AllocatorItem(B, 1000m, 20m), new AllocatorItem(C, 1000m, 50m) };

        var result = DeductionAllocator.Allocate(items, 600m);

        result.Items.ShouldBe(
        [
            new ItemAllocation(B, 20m, 0m, Removed: true),
            new ItemAllocation(C, 50m, 10m, Removed: false),
        ]);
        result.Deducted.ShouldBe(600m);
        result.Shortfall.ShouldBe(0m);
    }

    [Fact]
    public void Allocate_EqualPercents_TakesTheSmallerRemainingAmountFirst()
    {
        var items = new[] { new AllocatorItem(A, 1000m, 50m), new AllocatorItem(B, 200m, 50m) };

        var result = DeductionAllocator.Allocate(items, 50m);

        result.Items.ShouldBe([new ItemAllocation(B, 50m, 25m, Removed: false)]);
    }

    [Fact]
    public void Allocate_ExactlyEmpty_RemovesThePackage()
    {
        var result = DeductionAllocator.Allocate([new AllocatorItem(A, 500m, 40m)], 200m);

        result.Items.ShouldBe([new ItemAllocation(A, 40m, 0m, Removed: true)]);
        result.Deducted.ShouldBe(200m);
        result.Shortfall.ShouldBe(0m);
    }

    [Fact]
    public void Allocate_NotEnough_EmptiesEverythingAndReportsTheShortfall()
    {
        var items = new[] { new AllocatorItem(A, 330m, 50m), new AllocatorItem(B, 330m, 10m) };

        var result = DeductionAllocator.Allocate(items, 500m);

        result.Items.ShouldBe(
        [
            new ItemAllocation(B, 10m, 0m, Removed: true),
            new ItemAllocation(A, 50m, 0m, Removed: true),
        ]);
        result.Deducted.ShouldBe(198m);
        result.Shortfall.ShouldBe(302m);
        (result.Deducted + result.Shortfall).ShouldBe(result.Requested);
    }

    [Fact]
    public void Allocate_NoPackages_IsAllShortfall()
    {
        var result = DeductionAllocator.Allocate([], 120m);

        result.Items.ShouldBeEmpty();
        result.Deducted.ShouldBe(0m);
        result.Shortfall.ShouldBe(120m);
    }

    [Fact]
    public void Allocate_RemainderWithManyDecimals_RoundsPercentToTwoDecimals()
    {
        // 7 g of a full 300 g package leaves 293/300 = 97.666… % → 97.67.
        var result = DeductionAllocator.Allocate([new AllocatorItem(A, 300m, 100m)], 7m);

        result.Items.Single().NewPercent.ShouldBe(97.67m);
        result.Items.Single().Removed.ShouldBeFalse();
    }

    [Fact]
    public void Allocate_TinyRemainder_RoundsToZeroAndRemovesNeverNegative()
    {
        // 1000 ml at 10 % = 100 ml; taking 99.99 ml leaves 0.01 ml = 0.001 % → rounds to 0 → removed.
        var result = DeductionAllocator.Allocate([new AllocatorItem(A, 1000m, 10m)], 99.99m);

        var change = result.Items.Single();
        change.NewPercent.ShouldBe(0m);
        change.Removed.ShouldBeTrue();
        result.Shortfall.ShouldBe(0m);
    }

    [Fact]
    public void Allocate_TinyAmount_KeepsAtMostThePreviousPercent()
    {
        // 0.001 g of 1000 g is 0.0001 %: the rounded percent stays 50, never above it.
        var result = DeductionAllocator.Allocate([new AllocatorItem(A, 1000m, 50m)], 0.001m);

        result.Items.Single().NewPercent.ShouldBe(50m);
        result.Items.Single().NewPercent.ShouldBeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void Allocate_SkipsEmptyAndInvalidPackages()
    {
        var items = new[] { new AllocatorItem(A, 1000m, 0m), new AllocatorItem(B, 0m, 50m), new AllocatorItem(C, 100m, 100m) };

        var result = DeductionAllocator.Allocate(items, 10m);

        result.Items.ShouldBe([new ItemAllocation(C, 100m, 90m, Removed: false)]);
    }

    [Fact]
    public void Allocate_NonPositiveAmount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DeductionAllocator.Allocate([new AllocatorItem(A, 1m, 1m)], 0m));
    }
}
