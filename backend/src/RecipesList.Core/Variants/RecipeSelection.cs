namespace RecipesList.Core.Variants;

/// <summary>Whether a remembered pick was pinned by the user or chosen automatically.</summary>
public enum PickSource
{
    User,
    Auto,
}

/// <summary>A remembered pick for one slot (API.md §5.2).</summary>
public sealed record SelectionPick(string OptionKey, PickSource Source);

/// <summary>What a scaling choice is anchored to (API.md §5.2).</summary>
public enum ScalingKind
{
    /// <summary>"Mam: …" on an ingredient; the default, so selections stored before servings existed stay valid.</summary>
    Ingredient,

    /// <summary>A number of servings (PROJECT §5.7).</summary>
    Servings,
}

/// <summary>
/// The user's last scaling choice (the single one, PROJECT §5.7): either "Mam: …" on an ingredient, in base units
/// (<see cref="AnchorSlotKey"/> and <see cref="AnchorBaseAmount"/> set), or a number of servings (<see cref="Servings"/> set).
/// </summary>
public sealed record ScalingState(string? AnchorSlotKey, decimal? AnchorBaseAmount, ScalingKind Kind = ScalingKind.Ingredient, int? Servings = null)
{
    /// <summary>A "Mam: …" choice.</summary>
    public static ScalingState ForIngredient(string anchorSlotKey, decimal anchorBaseAmount) => new(anchorSlotKey, anchorBaseAmount);

    /// <summary>A servings choice.</summary>
    public static ScalingState ForServings(int servings) => new(null, null, ScalingKind.Servings, servings);
}

/// <summary>Selection memory of one user for one recipe (PROJECT §6.6); picks and scaling are jsonb.</summary>
public sealed class RecipeSelection
{
    public Guid UserId { get; set; }

    public Guid RecipeId { get; set; }

    /// <summary>Slot key → pick.</summary>
    public Dictionary<string, SelectionPick> Picks { get; set; } = new(StringComparer.Ordinal);

    public ScalingState? Scaling { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Cross-recipe preference for swapping one product for another (PROJECT §6.6).</summary>
public sealed class ProductPreference
{
    public Guid UserId { get; set; }

    public Guid FromProductId { get; set; }

    public Guid ToProductId { get; set; }

    /// <summary>The number of recipes in which the swap is pinned.</summary>
    public int Weight { get; set; }
}
