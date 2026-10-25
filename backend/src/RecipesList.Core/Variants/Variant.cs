using RecipesList.Core.Recipes;
using RecipesList.Core.Users;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Variants;

/// <summary>A variant: a list of operations against exactly one base recipe (PROJECT §6.1).</summary>
public sealed class Variant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public Guid AuthorId { get; set; }

    public User? Author { get; set; }

    /// <summary>Unique per recipe per author.</summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Evaluated against the variant's author; private by default (D-23).</summary>
    public VisibilityLevel Visibility { get; set; } = VisibilityLevel.Private;

    public List<IngredientOp> IngredientOps { get; set; } = [];

    public List<StepOp> StepOps { get; set; } = [];

    public List<Guid> UtensilIdsAdded { get; set; } = [];

    public List<Guid> UtensilIdsRemoved { get; set; } = [];

    /// <summary>Override of the base prep time; null inherits.</summary>
    public int? PrepMinutes { get; set; }

    /// <summary>Override of the base cook time; null inherits.</summary>
    public int? CookMinutes { get; set; }

    /// <summary>Override of the base servings (PROJECT §5.7); null inherits.</summary>
    public int? ServingsOverride { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
