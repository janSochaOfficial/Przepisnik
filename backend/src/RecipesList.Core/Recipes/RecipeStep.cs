namespace RecipesList.Core.Recipes;

/// <summary>Which list a step belongs to: Przygotowanie (prep) or Wykonanie (instructions).</summary>
public enum StepSection
{
    Prep,
    Instructions,
}

/// <summary>A prep or instruction step; its content blocks are stored as jsonb (TECHNICAL §3.4).</summary>
public sealed class RecipeStep
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RecipeId { get; set; }

    public StepSection Section { get; set; }

    /// <summary>0-based order within its section.</summary>
    public int Position { get; set; }

    public List<ContentBlock> Blocks { get; set; } = [];
}
