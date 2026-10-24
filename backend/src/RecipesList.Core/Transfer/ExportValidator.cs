using RecipesList.Core.Common;
using RecipesList.Core.Images;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Transfer;

/// <summary>
/// Structural and reference checks of an export file (API.md §8): format and version, unique keys, and every
/// reference (product, image, op target, op position) resolving. Content limits that the recipe and variant rules
/// enforce anyway are checked again when the recipe is created, inside the import transaction.
/// Errors are keyed by a JSON-like path with short codes, and thrown as 400 <c>import.invalid_format</c>.
/// </summary>
public static class ExportValidator
{
    public const int MaxVariantName = 80;
    public const int MaxDescription = 2000;

    /// <summary>Throws <c>import.unsupported_version</c> or <c>import.invalid_format</c>; returns when the file is consistent.</summary>
    public static void EnsureValid(RecipeExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Format != ExportFormat.Name)
        {
            throw Invalid(new() { ["format"] = ["invalid"] });
        }

        if (document.FormatVersion != ExportFormat.Version)
        {
            throw new DomainValidationException(
                ErrorCodes.ImportUnsupportedVersion,
                new Dictionary<string, string[]> { ["formatVersion"] = ["unsupported"] });
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Check(document, (path, code) => errors[path] = [code]);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(ErrorCodes.ImportInvalidFormat, errors);
        }
    }

    /// <summary>The image keys referenced by the file, in first-use order (cover, recipe steps, variant step ops).</summary>
    public static IReadOnlyList<string> ReferencedImageKeys(RecipeExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var keys = new List<string>();
        if (document.Recipe.CoverImageKey is { } cover)
        {
            keys.Add(cover);
        }

        foreach (var step in document.Recipe.PrepSteps.Concat(document.Recipe.InstructionSteps))
        {
            keys.AddRange(ImageKeys(step.Blocks));
        }

        foreach (var op in document.Variants.SelectMany(v => v.StepOps))
        {
            switch (op)
            {
                case ExportReplaceStepOp replace:
                    keys.AddRange(ImageKeys(replace.Blocks));
                    break;
                case ExportAddStepOp add:
                    keys.AddRange(ImageKeys(add.Blocks));
                    break;
                default:
                    break;
            }
        }

        return [.. keys.Distinct(StringComparer.Ordinal)];
    }

    private static DomainValidationException Invalid(Dictionary<string, string[]> errors) => new(ErrorCodes.ImportInvalidFormat, errors);

    private static IEnumerable<string> ImageKeys(IReadOnlyList<ExportBlock> blocks) =>
        blocks.OfType<ExportImageBlock>().Select(b => b.ImageKey);

    private static void Check(RecipeExportDocument document, Action<string, string> fail)
    {
        var productKeys = new HashSet<string>(StringComparer.Ordinal);
        var productNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < document.Products.Count; i++)
        {
            var product = document.Products[i];
            if (string.IsNullOrEmpty(product.Key) || !productKeys.Add(product.Key))
            {
                fail($"products[{i}].key", "duplicate");
            }

            if (!productNames.Add(NameNormalizer.Normalize(product.Name ?? "")))
            {
                fail($"products[{i}].name", "duplicate");
            }
        }

        foreach (var (key, image) in document.Images)
        {
            if (image.ContentType != ExportFormat.ImageContentType)
            {
                fail($"images.{key}.contentType", "invalid");
            }
            else if (!IsBase64(image.DataBase64))
            {
                fail($"images.{key}.dataBase64", "invalid");
            }
        }

        var recipe = document.Recipe;
        if (recipe.CoverImageKey is { } cover && !document.Images.ContainsKey(cover))
        {
            fail("recipe.coverImageKey", "unknown_key");
        }

        var ingredientKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < recipe.Ingredients.Count; i++)
        {
            var ingredient = recipe.Ingredients[i];
            if (string.IsNullOrEmpty(ingredient.Key) || !ingredientKeys.Add(ingredient.Key))
            {
                fail($"recipe.ingredients[{i}].key", "duplicate");
            }

            if (!productKeys.Contains(ingredient.ProductKey))
            {
                fail($"recipe.ingredients[{i}].productKey", "unknown_key");
            }
        }

        var stepKeys = new Dictionary<string, StepSection>(StringComparer.Ordinal);
        RegisterSteps("recipe.prepSteps", recipe.PrepSteps, StepSection.Prep);
        RegisterSteps("recipe.instructionSteps", recipe.InstructionSteps, StepSection.Instructions);

        for (var i = 0; i < document.Variants.Count; i++)
        {
            CheckVariant(document, document.Variants[i], $"variants[{i}]", productKeys, ingredientKeys, stepKeys, fail);
        }

        void RegisterSteps(string path, IReadOnlyList<ExportStep> steps, StepSection section)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (string.IsNullOrEmpty(steps[i].Key) || !stepKeys.TryAdd(steps[i].Key, section))
                {
                    fail($"{path}[{i}].key", "duplicate");
                }

                CheckBlocks(document, steps[i].Blocks, $"{path}[{i}].blocks", fail);
            }
        }
    }

    private static void CheckVariant(
        RecipeExportDocument document,
        ExportVariant variant,
        string path,
        HashSet<string> productKeys,
        HashSet<string> baseIngredientKeys,
        Dictionary<string, StepSection> baseStepKeys,
        Action<string, string> fail)
    {
        var name = (variant.Name ?? "").Trim();
        if (name.Length is < 1 or > MaxVariantName)
        {
            fail($"{path}.name", "out_of_range");
        }

        var ingredientsAdded = new HashSet<string>(StringComparer.Ordinal);
        var stepsAdded = new Dictionary<string, StepSection>(StringComparer.Ordinal);

        for (var i = 0; i < variant.IngredientOps.Count; i++)
        {
            var at = $"{path}.ingredientOps[{i}]";
            switch (variant.IngredientOps[i])
            {
                case ExportModifyIngredientOp modify:
                    RequireKey(baseIngredientKeys, modify.TargetKey, $"{at}.targetKey");
                    RequireKey(productKeys, modify.ProductKey, $"{at}.productKey");
                    break;
                case ExportRemoveIngredientOp remove:
                    RequireKey(baseIngredientKeys, remove.TargetKey, $"{at}.targetKey");
                    break;
                case ExportAddIngredientOp add:
                    RequireKey(productKeys, add.ProductKey, $"{at}.productKey");
                    if (add.AfterKey is { } after && !baseIngredientKeys.Contains(after) && !ingredientsAdded.Contains(after))
                    {
                        fail($"{at}.afterKey", "unknown_key");
                    }

                    if (string.IsNullOrEmpty(add.Key) || baseIngredientKeys.Contains(add.Key) || !ingredientsAdded.Add(add.Key))
                    {
                        fail($"{at}.key", "duplicate");
                    }

                    break;
                default:
                    fail(at, "invalid");
                    break;
            }
        }

        for (var i = 0; i < variant.StepOps.Count; i++)
        {
            var at = $"{path}.stepOps[{i}]";
            var op = variant.StepOps[i];
            switch (op)
            {
                case ExportReplaceStepOp replace:
                    RequireStep(replace.TargetKey, op.Section, $"{at}.targetKey");
                    CheckBlocks(document, replace.Blocks, $"{at}.blocks", fail);
                    break;
                case ExportRemoveStepOp remove:
                    RequireStep(remove.TargetKey, op.Section, $"{at}.targetKey");
                    break;
                case ExportAddStepOp add:
                    if (add.AfterKey is { } after
                        && !(baseStepKeys.TryGetValue(after, out var baseSection) && baseSection == op.Section)
                        && !(stepsAdded.TryGetValue(after, out var addedSection) && addedSection == op.Section))
                    {
                        fail($"{at}.afterKey", "unknown_key");
                    }

                    if (string.IsNullOrEmpty(add.Key) || baseStepKeys.ContainsKey(add.Key) || !stepsAdded.TryAdd(add.Key, op.Section))
                    {
                        fail($"{at}.key", "duplicate");
                    }

                    CheckBlocks(document, add.Blocks, $"{at}.blocks", fail);
                    break;
                default:
                    fail(at, "invalid");
                    break;
            }
        }

        void RequireKey(HashSet<string> keys, string key, string keyPath)
        {
            if (!keys.Contains(key))
            {
                fail(keyPath, "unknown_key");
            }
        }

        void RequireStep(string key, StepSection section, string keyPath)
        {
            if (!(baseStepKeys.TryGetValue(key, out var actual) && actual == section))
            {
                fail(keyPath, "unknown_key");
            }
        }
    }

    private static void CheckBlocks(RecipeExportDocument document, IReadOnlyList<ExportBlock> blocks, string path, Action<string, string> fail)
    {
        if (blocks.Count == 0)
        {
            fail(path, "required");
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            switch (blocks[i])
            {
                case ExportTextBlock { Text: null or { Length: 0 } or { Length: > RecipeInputValidator.MaxText } }:
                    fail($"{path}[{i}].text", "out_of_range");
                    break;
                case ExportImageBlock image:
                    if (!document.Images.ContainsKey(image.ImageKey))
                    {
                        fail($"{path}[{i}].imageKey", "unknown_key");
                    }
                    else if (image.Caption is { Length: > RecipeInputValidator.MaxCaption })
                    {
                        fail($"{path}[{i}].caption", "too_long");
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static bool IsBase64(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return false;
        }

        var buffer = new byte[data.Length * 3 / 4 + 3];
        return Convert.TryFromBase64String(data, buffer, out var written) && written > 0 && written <= ImageLimits.MaxUploadBytes;
    }
}
