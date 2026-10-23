namespace RecipesList.Core.Fridges;

/// <summary>A recorded deduction of a cooked recipe from a fridge, with what is needed to undo it (PROJECT §9).</summary>
public sealed class Deduction
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FridgeId { get; set; }

    public Guid UserId { get; set; }

    public Guid? RecipeId { get; set; }

    public Guid? VariantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UndoneAt { get; set; }

    /// <summary>Per-product results with full item snapshots (jsonb).</summary>
    public List<DeductionLine> Lines { get; set; } = [];
}

/// <summary>The deduction of one product.</summary>
public sealed record DeductionLine(
    Guid ProductId,
    decimal Requested,
    decimal Deducted,
    decimal Shortfall,
    IReadOnlyList<DeductionItemChange> Items);

/// <summary>
/// What happened to one fridge item, plus its state before (for undo). <c>VersionAfter</c> is the item's
/// <c>xmin</c> after the deduction, or null when the item was removed.
/// </summary>
public sealed record DeductionItemChange(
    Guid ItemId,
    decimal PreviousPercent,
    decimal NewPercent,
    bool Removed,
    uint? VersionAfter,
    FridgeItemSnapshot Before);

/// <summary>A full copy of a fridge item, enough to re-create it under the same id.</summary>
public sealed record FridgeItemSnapshot(
    Guid ProductId,
    decimal PackageAmount,
    decimal RemainingPercent,
    string? Note,
    Guid AddedById,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
