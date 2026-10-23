using RecipesList.Core.Catalog;
using RecipesList.Core.Images;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Recipes;

/// <summary>A base recipe (PROJECT §4).</summary>
public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AuthorId { get; set; }

    public User? Author { get; set; }

    public required string Title { get; set; }

    /// <summary><see cref="Common.NameNormalizer"/> form of <see cref="Title"/>; trigram-indexed for search.</summary>
    public required string NormalizedTitle { get; set; }

    public string? Description { get; set; }

    public Guid? CoverImageId { get; set; }

    public Image? CoverImage { get; set; }

    public int? PrepMinutes { get; set; }

    public int? CookMinutes { get; set; }

    public VisibilityLevel Visibility { get; set; } = VisibilityLevel.Private;

    /// <summary>Set instead of deleting when other users have variants (PROJECT §4.4, D-17).</summary>
    public bool IsArchived { get; set; }

    /// <summary>How many times it was deducted from a fridge.</summary>
    public int CookCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }

    public List<RecipeIngredient> Ingredients { get; set; } = [];

    /// <summary>Prep and instruction steps, told apart by <see cref="RecipeStep.Section"/>.</summary>
    public List<RecipeStep> Steps { get; set; } = [];

    public List<RecipeTag> Tags { get; set; } = [];

    public List<RecipeUtensil> Utensils { get; set; } = [];

    public List<Variant> Variants { get; set; } = [];
}

/// <summary>Join row between a recipe and a tag.</summary>
public sealed class RecipeTag
{
    public Guid RecipeId { get; set; }

    public Guid TagId { get; set; }

    public Tag? Tag { get; set; }
}

/// <summary>Join row between a recipe and a utensil.</summary>
public sealed class RecipeUtensil
{
    public Guid RecipeId { get; set; }

    public Guid UtensilId { get; set; }

    public Utensil? Utensil { get; set; }
}
