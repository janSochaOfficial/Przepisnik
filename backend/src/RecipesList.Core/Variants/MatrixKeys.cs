using System.Globalization;

namespace RecipesList.Core.Variants;

/// <summary>The slot and option keys of the variant matrix (API.md §5.1), shared by the engine and selection memory.</summary>
public static class MatrixKeys
{
    /// <summary>The option of a slot whose ingredient is absent or removed ("brak").</summary>
    public const string None = "none";

    /// <summary>The API id of the base recipe column.</summary>
    public const string BaseVariantId = "base";

    private const string BaseSlotPrefix = "i:";
    private const string AddedSlotPrefix = "p:";
    private const string NullAmount = "x";

    /// <summary>The key of a base ingredient slot: <c>i:&lt;ingredientId&gt;</c>.</summary>
    public static string BaseSlot(Guid ingredientId) => BaseSlotPrefix + ingredientId.ToString("D");

    /// <summary>The key of a slot for a product added by variants: <c>p:&lt;productId&gt;</c>.</summary>
    public static string AddedSlot(Guid productId) => AddedSlotPrefix + productId.ToString("D");

    /// <summary>
    /// The key of a product option: <c>&lt;productId&gt;:&lt;amount&gt;</c>, the amount rounded to 3 decimals in invariant
    /// format without trailing zeros (<c>…:27.3</c>, <c>…:200</c>), or <c>x</c> for "do smaku".
    /// </summary>
    public static string Option(Guid productId, decimal? baseAmount) =>
        productId.ToString("D") + ":" + FormatAmount(baseAmount);

    /// <summary>Whether <paramref name="key"/> is a well-formed slot key (<c>i:&lt;uuid&gt;</c> or <c>p:&lt;uuid&gt;</c>).</summary>
    public static bool IsWellFormedSlot(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return (key.StartsWith(BaseSlotPrefix, StringComparison.Ordinal) || key.StartsWith(AddedSlotPrefix, StringComparison.Ordinal))
            && Guid.TryParseExact(key.AsSpan(2), "D", out _);
    }

    /// <summary>The ingredient id of a base slot key, or null for any other key.</summary>
    public static Guid? BaseSlotIngredientId(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.StartsWith(BaseSlotPrefix, StringComparison.Ordinal) && Guid.TryParseExact(key.AsSpan(2), "D", out var id)
            ? id
            : null;
    }

    /// <summary>The product of a product option key, or null for <c>none</c> and malformed keys.</summary>
    public static Guid? OptionProductId(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var colon = key.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && Guid.TryParseExact(key.AsSpan(0, colon), "D", out var id) ? id : null;
    }

    private static string FormatAmount(decimal? amount) =>
        amount is { } value
            ? Math.Round(value, 3, MidpointRounding.AwayFromZero).ToString("0.###", CultureInfo.InvariantCulture)
            : NullAmount;
}
