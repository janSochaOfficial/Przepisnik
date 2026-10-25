using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;
using RecipesList.Core.Units;

namespace RecipesList.Core.ShoppingLists;

/// <summary>An entry of a fridge's shopping list: a catalog product (with an optional amount) or free text (PROJECT §10).</summary>
public sealed class ShoppingListItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FridgeId { get; set; }

    /// <summary>Exactly one of <see cref="ProductId"/> and <see cref="Text"/> is set.</summary>
    public Guid? ProductId { get; set; }

    public Product? Product { get; set; }

    public string? Text { get; set; }

    /// <summary>Base units; only with a product. Derived from the quantity when one is set.</summary>
    public decimal? Amount { get; set; }

    /// <summary>The amount as the user chose it (PROJECT §10); null together with <see cref="QuantityUnit"/> means no chosen unit.</summary>
    public decimal? QuantityValue { get; set; }

    public Unit? QuantityUnit { get; set; }

    public PieceSize? QuantitySize { get; set; }

    /// <summary>The custom unit of the quantity; set iff <see cref="QuantityUnit"/> is <see cref="Unit.Custom"/>.</summary>
    public Guid? QuantityCustomUnitId { get; set; }

    public CustomUnit? CustomUnit { get; set; }

    /// <summary>The chosen quantity, or null when the amount has no chosen unit.</summary>
    public Quantity? Quantity
    {
        get => QuantityValue is { } value && QuantityUnit is { } unit ? new Quantity(value, unit, QuantitySize, QuantityCustomUnitId) : null;
        set
        {
            QuantityValue = value?.Value;
            QuantityUnit = value?.Unit;
            QuantitySize = value?.Size;
            QuantityCustomUnitId = value?.CustomUnitId;
        }
    }

    public bool Checked { get; set; }

    public Guid AddedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
