using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;

namespace RecipesList.Core.Recipes;

/// <summary>An ingredient row of a base recipe (PROJECT §4.2). Ids are stable across edits (API.md §4).</summary>
public sealed class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RecipeId { get; set; }

    /// <summary>0-based order within the recipe.</summary>
    public int Position { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Null together with <see cref="QuantityUnit"/> means "do smaku".</summary>
    public decimal? QuantityValue { get; set; }

    public Unit? QuantityUnit { get; set; }

    public PieceSize? QuantitySize { get; set; }

    /// <summary>Canonical amount (g, ml or medium pieces); null iff there is no quantity.</summary>
    public decimal? BaseAmount { get; set; }

    public string? Note { get; set; }

    public bool Optional { get; set; }

    /// <summary>The entered quantity, or null for "do smaku".</summary>
    public Quantity? Quantity
    {
        get => QuantityValue is { } value && QuantityUnit is { } unit ? new Quantity(value, unit, QuantitySize) : null;
        set
        {
            QuantityValue = value?.Value;
            QuantityUnit = value?.Unit;
            QuantitySize = value?.Size;
        }
    }
}
