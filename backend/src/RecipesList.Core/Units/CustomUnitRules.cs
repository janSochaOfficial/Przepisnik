using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Units;

/// <summary>The writable fields of a custom unit (API.md §2.1 <c>CustomUnitWrite</c>): "1 <c>Name</c> = <c>EqualTo</c>" (JSON <c>equals</c>).</summary>
public sealed record CustomUnitInput(string? Name, PluralForms? Forms, Guid? ProductId, Quantity? EqualTo);

/// <summary>A validated unit: the cleaned name and forms, and the definition in base units.</summary>
public sealed record CustomUnitDefinition(string Name, string NormalizedName, PluralForms Forms, MeasureKind MeasureKind, decimal Amount);

/// <summary>Pure rules of the custom-unit catalog (PROJECT §5.6, D-30).</summary>
public static class CustomUnitRules
{
    public const int MaxName = 40;
    public const int MaxForm = 40;

    /// <summary>Amounts that differ by at most this fraction are the same amount (import matching, API.md §8).</summary>
    public const decimal SameAmountTolerance = 0.01m;

    /// <summary>
    /// Validates the shape (400 <c>validation.failed</c> with field codes) and computes the definition. For a product
    /// unit the amount is the base amount of <c>equals</c> for <paramref name="product"/>, in its measure kind; when
    /// that is impossible → 400 <c>unit.invalid</c>. For a unit for every product (<paramref name="product"/> null) it
    /// is the native amount of <c>equals</c> in its own kind, sizes ignored.
    /// </summary>
    public static CustomUnitDefinition Define(CustomUnitInput input, Product? product, IQuantityConverter converter)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(converter);
        var errors = new ValidationErrors();

        var name = CleanText(input.Name);
        CheckLength(errors, "name", name, MaxName);

        PluralForms? forms = null;
        if (input.Forms is null)
        {
            errors.Add("forms", "required");
        }
        else
        {
            forms = new PluralForms(CleanText(input.Forms.One), CleanText(input.Forms.Few), CleanText(input.Forms.Many), CleanText(input.Forms.Fraction));
            CheckLength(errors, "forms.one", forms.One, MaxForm);
            CheckLength(errors, "forms.few", forms.Few, MaxForm);
            CheckLength(errors, "forms.many", forms.Many, MaxForm);
            CheckLength(errors, "forms.fraction", forms.Fraction, MaxForm);
        }

        var equals = input.EqualTo;
        if (equals is null)
        {
            errors.Add("equals", "required");
        }
        else if (equals.Unit == Unit.Custom || equals.CustomUnitId is not null)
        {
            errors.Add("equals.unit", "invalid");
        }
        else if (!equals.IsWellFormed)
        {
            errors.Add("equals", ErrorCodes.QuantityInvalid);
        }

        errors.ThrowIfAny();

        MeasureKind kind;
        decimal amount;
        if (product is not null)
        {
            amount = converter.ToBaseAmount(equals, product)
                ?? throw new DomainValidationException(ErrorCodes.UnitInvalid, new Dictionary<string, string[]> { ["equals"] = [ErrorCodes.UnitInvalid] });
            kind = product.MeasureKind;
        }
        else
        {
            var native = QuantityConverter.NativeAmount(equals!, useSizes: false)!.Value;
            kind = native.Kind;
            amount = QuantityConverter.RoundBase(native.Amount);
        }

        if (amount <= 0)
        {
            throw new DomainValidationException(ErrorCodes.UnitInvalid, new Dictionary<string, string[]> { ["equals"] = [ErrorCodes.UnitInvalid] });
        }

        return new CustomUnitDefinition(name, NameNormalizer.Normalize(name), forms!, kind, amount);
    }

    /// <summary>Whether two amounts are equal within <see cref="SameAmountTolerance"/> of the larger one.</summary>
    public static bool IsSameAmount(decimal a, decimal b) =>
        Math.Abs(a - b) <= SameAmountTolerance * Math.Max(Math.Abs(a), Math.Abs(b));

    /// <summary>
    /// The first of <c>name</c>, <c>name (2)</c>, <c>name (3)</c> … whose normalized form is not in
    /// <paramref name="takenNormalized"/>; the base is shortened so the result fits <see cref="MaxName"/>.
    /// </summary>
    public static string UniqueName(string name, IReadOnlySet<string> takenNormalized)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(takenNormalized);
        var candidate = name;
        for (var n = 2; takenNormalized.Contains(NameNormalizer.Normalize(candidate)); n++)
        {
            var suffix = $" ({n})";
            var stem = name.Length + suffix.Length > MaxName ? name[..(MaxName - suffix.Length)].TrimEnd() : name;
            candidate = stem + suffix;
        }

        return candidate;
    }

    private static string CleanText(string? text) => ProductRules.CleanName(text ?? "");

    private static void CheckLength(ValidationErrors errors, string path, string value, int max)
    {
        if (value.Length == 0)
        {
            errors.Add(path, "required");
        }
        else if (value.Length > max)
        {
            errors.Add(path, "too_long");
        }
    }
}
