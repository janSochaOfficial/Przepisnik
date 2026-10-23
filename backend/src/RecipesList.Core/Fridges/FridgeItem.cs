using RecipesList.Core.Catalog;

namespace RecipesList.Core.Fridges;

/// <summary>One package of a product in a fridge (PROJECT §8.2).</summary>
public sealed class FridgeItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FridgeId { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Package size in base units (&gt; 0).</summary>
    public decimal PackageAmount { get; set; }

    /// <summary>0–100 with up to 2 decimals.</summary>
    public decimal RemainingPercent { get; set; }

    public string? Note { get; set; }

    public Guid AddedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
