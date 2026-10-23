using System.Text.Json.Serialization;

namespace RecipesList.Core.Recipes;

/// <summary>
/// A piece of step content (PROJECT §4.1): text or a photo. Stored as jsonb on <c>recipe_steps.blocks</c> and inside
/// variant step ops, using the API shape (<c>{"type":"text","text":…}</c> / <c>{"type":"image","imageId":…}</c>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ImageBlock), "image")]
public abstract record ContentBlock;

/// <summary>Plain text; line breaks are kept. At most 5000 characters.</summary>
public sealed record TextBlock(string Text) : ContentBlock;

/// <summary>A photo with an optional caption (at most 300 characters).</summary>
public sealed record ImageBlock(Guid ImageId, string? Caption) : ContentBlock;
