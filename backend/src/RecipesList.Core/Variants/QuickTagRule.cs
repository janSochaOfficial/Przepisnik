namespace RecipesList.Core.Variants;

/// <summary>The rule of the computed system tag <c>szybkie</c> (PROJECT §4.3, D-09).</summary>
public static class QuickTagRule
{
    /// <summary>The longest total time, in minutes, that still counts as quick.</summary>
    public const int MaxTotalMinutes = 30;

    /// <summary>At least one time is set and prep + cook ≤ 30 minutes, a missing time counting as 0.</summary>
    public static bool Applies(int? prepMinutes, int? cookMinutes) =>
        (prepMinutes is not null || cookMinutes is not null) && (prepMinutes ?? 0) + (cookMinutes ?? 0) <= MaxTotalMinutes;
}
