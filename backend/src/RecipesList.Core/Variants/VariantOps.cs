using System.Text.Json.Serialization;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Variants;

/// <summary>
/// An ingredient operation of a variant (API.md §5). Stored as a jsonb array on <c>variants.ingredient_ops</c>
/// in the API shape, so SQL can re-point <c>productId</c> fields (B2 merge).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ModifyIngredientOp), "modify")]
[JsonDerivedType(typeof(RemoveIngredientOp), "remove")]
[JsonDerivedType(typeof(AddIngredientOp), "add")]
public abstract record IngredientOp;

/// <summary>
/// Changes the product, quantity, note or optional flag of a base ingredient. <see cref="BaseAmount"/> is computed by
/// the server when the op is stored (it is not part of the API op shape), so resolving needs no product lookups.
/// </summary>
public sealed record ModifyIngredientOp(Guid TargetId, Guid ProductId, Quantity? Quantity, decimal? BaseAmount, string? Note, bool Optional) : IngredientOp;

/// <summary>Removes a base ingredient.</summary>
public sealed record RemoveIngredientOp(Guid TargetId) : IngredientOp;

/// <summary>
/// Adds an ingredient after <see cref="AfterId"/> (a base row or an earlier add); null means at the start.
/// <see cref="BaseAmount"/> is server-computed, as for <see cref="ModifyIngredientOp"/>.
/// </summary>
public sealed record AddIngredientOp(Guid Id, Guid? AfterId, Guid ProductId, Quantity? Quantity, decimal? BaseAmount, string? Note, bool Optional) : IngredientOp;

/// <summary>A step operation of a variant (API.md §5), stored as jsonb on <c>variants.step_ops</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ReplaceStepOp), "replace")]
[JsonDerivedType(typeof(RemoveStepOp), "remove")]
[JsonDerivedType(typeof(AddStepOp), "add")]
public abstract record StepOp(StepSection Section);

/// <summary>Replaces the content of a base step.</summary>
public sealed record ReplaceStepOp(StepSection Section, Guid TargetId, IReadOnlyList<ContentBlock> Blocks) : StepOp(Section);

/// <summary>Removes a base step.</summary>
public sealed record RemoveStepOp(StepSection Section, Guid TargetId) : StepOp(Section);

/// <summary>Adds a step after <see cref="AfterId"/> (a base step or an earlier add); null means at the start.</summary>
public sealed record AddStepOp(StepSection Section, Guid Id, Guid? AfterId, IReadOnlyList<ContentBlock> Blocks) : StepOp(Section);
