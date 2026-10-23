namespace RecipesList.Core.Catalog;

/// <summary>A recipe tag from the shared catalog (PROJECT §4.3).</summary>
public sealed class Tag
{
    /// <summary>The computed system tag (D-09); never chosen by hand.</summary>
    public const string QuickTagName = "szybkie";

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public bool IsSystem { get; set; }
}
