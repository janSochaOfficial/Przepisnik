using RecipesList.Core.Catalog;

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

    /// <summary>Base units; only with a product.</summary>
    public decimal? Amount { get; set; }

    public bool Checked { get; set; }

    public Guid AddedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
