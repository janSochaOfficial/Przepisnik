namespace RecipesList.Core.Catalog;

/// <summary>A kitchen utensil from the shared catalog ("Przybory", PROJECT §4.1).</summary>
public sealed class Utensil
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }
}
