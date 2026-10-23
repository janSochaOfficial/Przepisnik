namespace RecipesList.Core.Variants;

/// <summary>Whether a remembered pick was pinned by the user or chosen automatically.</summary>
public enum PickSource
{
    User,
    Auto,
}

/// <summary>A remembered pick for one slot (API.md §5.2).</summary>
public sealed record SelectionPick(string OptionKey, PickSource Source);

/// <summary>The user's last "Mam: …" scaling choice, in base units.</summary>
public sealed record ScalingState(string AnchorSlotKey, decimal AnchorBaseAmount);

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
