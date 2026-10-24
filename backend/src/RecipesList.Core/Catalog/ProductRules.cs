using RecipesList.Core.Common;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Catalog;

/// <summary>The writable fields of a product (API.md §2 <c>ProductWrite</c>).</summary>
public sealed record ProductInput(
    string Name,
    ProductCategory Category,
    MeasureKind MeasureKind,
    DisplayClass DisplayClass,
    bool HasSizes,
    bool AllowHalf,
    decimal? GramsPerMl,
    decimal? GramsPerPiece,
    decimal? DefaultPackageAmount,
    PluralForms? PieceForms);

/// <summary>Pure rules of the catalog: name cleaning and product validation (PROJECT §7, API.md §2).</summary>
public static class ProductRules
{
    public const int MinProductName = 2;
    public const int MaxProductName = 80;
    public const int MaxTagOrUtensilName = 60;
    public const int MaxPieceForm = 40;

    /// <summary>Trims and collapses inner whitespace to single spaces; case and diacritics are kept for display.</summary>
    public static string CleanName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Whether the display class fits the measure kind (PROJECT §5.1).</summary>
    public static bool Fits(DisplayClass displayClass, MeasureKind measureKind) => displayClass switch
    {
        DisplayClass.Weight => measureKind == MeasureKind.Mass,
        DisplayClass.Liquid or DisplayClass.Spice => measureKind == MeasureKind.Volume,
        DisplayClass.Piece => measureKind == MeasureKind.Count,
        _ => false,
    };

    /// <summary>
    /// Validates a product. A shape problem → 400 <c>validation.failed</c> with field codes; a bad combination of
    /// kind, class, sizes and half pieces → 400 <c>product.invalid_combination</c>.
    /// </summary>
    public static void Validate(ProductInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new ValidationErrors();

        var length = CleanName(input.Name ?? "").Length;
        if (length == 0)
        {
            errors.Add("name", "required");
        }
        else if (length is < MinProductName or > MaxProductName)
        {
            errors.Add("name", "out_of_range");
        }

        CheckPositive(errors, "gramsPerMl", input.GramsPerMl);
        CheckPositive(errors, "gramsPerPiece", input.GramsPerPiece);
        CheckPositive(errors, "defaultPackageAmount", input.DefaultPackageAmount);

        if (input.PieceForms is { } forms)
        {
            CheckForm(errors, "pieceForms.one", forms.One);
            CheckForm(errors, "pieceForms.few", forms.Few);
            CheckForm(errors, "pieceForms.many", forms.Many);
            CheckForm(errors, "pieceForms.fraction", forms.Fraction);
        }

        errors.ThrowIfAny();

        var combination = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!Fits(input.DisplayClass, input.MeasureKind))
        {
            combination["displayClass"] = ["does_not_fit_measure_kind"];
        }

        if (input.MeasureKind != MeasureKind.Count && input.HasSizes)
        {
            combination["hasSizes"] = ["count_only"];
        }

        if (input.MeasureKind != MeasureKind.Count && input.AllowHalf)
        {
            combination["allowHalf"] = ["count_only"];
        }

        if (combination.Count > 0)
        {
            throw new DomainValidationException(ErrorCodes.ProductInvalidCombination, combination);
        }
    }

    /// <summary>Validates and cleans the name of a tag or utensil, returning the display name.</summary>
    public static string ValidateSimpleName(string? name)
    {
        var clean = CleanName(name ?? "");
        var errors = new ValidationErrors();
        if (clean.Length == 0)
        {
            errors.Add("name", "required");
        }
        else if (clean.Length > MaxTagOrUtensilName)
        {
            errors.Add("name", "too_long");
        }

        errors.ThrowIfAny();
        return clean;
    }

    private static void CheckPositive(ValidationErrors errors, string path, decimal? value)
    {
        if (value is <= 0)
        {
            errors.Add(path, "must_be_positive");
        }
    }

    private static void CheckForm(ValidationErrors errors, string path, string? form)
    {
        if (string.IsNullOrWhiteSpace(form))
        {
            errors.Add(path, "required");
        }
        else if (form.Length > MaxPieceForm)
        {
            errors.Add(path, "too_long");
        }
    }
}
