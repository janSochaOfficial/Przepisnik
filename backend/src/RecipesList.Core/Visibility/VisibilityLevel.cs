namespace RecipesList.Core.Visibility;

/// <summary>
/// Who can see a recipe or variant (PROJECT §4.4, §6.1, D-21); the API calls this type <c>Visibility</c>.
/// (Named <c>VisibilityLevel</c> in C# so it does not clash with the <c>RecipesList.Core.Visibility</c> namespace.)
/// </summary>
public enum VisibilityLevel
{
    /// <summary>Only the author.</summary>
    Private,

    /// <summary>The author and everyone who shares at least one fridge with the author (D-22).</summary>
    Fridges,

    /// <summary>Every logged-in user.</summary>
    Public,
}
