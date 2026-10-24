namespace RecipesList.Core.Variants;

/// <summary>
/// How many times a variant was deducted from a fridge. Kept apart from <see cref="Variant"/> so that cooking never
/// changes the variant's row version (D-28). No row means zero. Deleted with the variant.
/// </summary>
public sealed class VariantCookCount
{
    public Guid VariantId { get; set; }

    public int Count { get; set; }
}
