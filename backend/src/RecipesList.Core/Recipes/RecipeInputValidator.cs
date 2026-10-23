using RecipesList.Core.Common;
using RecipesList.Core.Quantities;

namespace RecipesList.Core.Recipes;

/// <summary>
/// Shape validation of a <see cref="RecipeInput"/> (limits from API.md §4 and PROJECT §4.1). Reference checks that
/// need the database (products, tags, images, unit conversion) live in <see cref="RecipeService"/>.
/// Field errors are keyed by camelCase path (<c>ingredients[3].quantity</c>) with short codes.
/// </summary>
public static class RecipeInputValidator
{
    public const int MaxTitle = 150;
    public const int MaxDescription = 2000;
    public const int MaxMinutes = 2880;
    public const int MaxIngredients = 100;
    public const int MaxSteps = 50;
    public const int MaxNote = 300;
    public const int MaxText = 5000;
    public const int MaxCaption = 300;

    /// <summary>Returns the shape errors; empty when the input is valid.</summary>
    public static ValidationErrors Validate(RecipeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            errors.Add("title", "required");
        }
        else if (input.Title.Trim().Length > MaxTitle)
        {
            errors.Add("title", "too_long");
        }

        if (input.Description is { Length: > MaxDescription })
        {
            errors.Add("description", "too_long");
        }

        CheckMinutes(errors, "prepMinutes", input.PrepMinutes);
        CheckMinutes(errors, "cookMinutes", input.CookMinutes);

        if (input.Ingredients.Count is < 1 or > MaxIngredients)
        {
            errors.Add("ingredients", "out_of_range");
        }

        for (var i = 0; i < input.Ingredients.Count; i++)
        {
            var ingredient = input.Ingredients[i];
            if (ingredient.Note is { Length: > MaxNote })
            {
                errors.Add($"ingredients[{i}].note", "too_long");
            }

            if (ingredient.Quantity is { } q && (q.Value <= 0 || (q.Size is not null && q.Unit != Unit.Piece)))
            {
                errors.Add($"ingredients[{i}].quantity", ErrorCodes.QuantityInvalid);
            }
        }

        CheckSteps(errors, "prepSteps", input.PrepSteps, minimum: 0);
        CheckSteps(errors, "instructionSteps", input.InstructionSteps, minimum: 1);
        return errors;
    }

    private static void CheckMinutes(ValidationErrors errors, string path, int? minutes)
    {
        if (minutes is < 0 or > MaxMinutes)
        {
            errors.Add(path, "out_of_range");
        }
    }

    private static void CheckSteps(ValidationErrors errors, string path, IReadOnlyList<StepInput> steps, int minimum)
    {
        if (steps.Count < minimum || steps.Count > MaxSteps)
        {
            errors.Add(path, "out_of_range");
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var blocks = steps[i].Blocks;
            if (blocks.Count == 0)
            {
                errors.Add($"{path}[{i}].blocks", "required");
            }

            for (var j = 0; j < blocks.Count; j++)
            {
                switch (blocks[j])
                {
                    case TextBlock { Text.Length: 0 }:
                        errors.Add($"{path}[{i}].blocks[{j}].text", "required");
                        break;
                    case TextBlock { Text.Length: > MaxText }:
                        errors.Add($"{path}[{i}].blocks[{j}].text", "too_long");
                        break;
                    case ImageBlock { Caption.Length: > MaxCaption }:
                        errors.Add($"{path}[{i}].blocks[{j}].caption", "too_long");
                        break;
                    default:
                        break;
                }
            }
        }
    }
}

/// <summary>Collects field errors and turns them into a <see cref="DomainValidationException"/>.</summary>
public sealed class ValidationErrors
{
    private static readonly string[] SpecificCodes =
        [ErrorCodes.QuantityUnitIncompatible, ErrorCodes.QuantityInvalid, ErrorCodes.ImageNotOwned];

    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public void Add(string path, string code)
    {
        if (!_errors.TryGetValue(path, out var list))
        {
            _errors[path] = list = [];
        }

        if (!list.Contains(code, StringComparer.Ordinal))
        {
            list.Add(code);
        }
    }

    /// <summary>
    /// Throws when there are errors. The top-level code is the one specific code (e.g. <c>image.not_owned</c>)
    /// when every error carries it, otherwise <c>validation.failed</c>.
    /// </summary>
    public void ThrowIfAny()
    {
        if (_errors.Count == 0)
        {
            return;
        }

        var distinct = _errors.Values.SelectMany(v => v).Distinct(StringComparer.Ordinal).ToList();
        var code = distinct is [var only] && SpecificCodes.Contains(only, StringComparer.Ordinal)
            ? only
            : ErrorCodes.ValidationFailed;
        throw new DomainValidationException(code, _errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal));
    }
}
