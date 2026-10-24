using System.Text.RegularExpressions;
using RecipesList.Core.Common;

namespace RecipesList.Core.Users;

/// <summary>
/// Nickname rules (PROJECT §2): 3–30 letters (Polish letters included), digits, <c>_</c> or <c>-</c>, unique
/// case-insensitively. Surrounding whitespace is trimmed before the rules apply.
/// </summary>
public static partial class NicknameRules
{
    public const int MinLength = 3;

    public const int MaxLength = 30;

    /// <summary>Whether the nickname, after trimming, is well formed. Uniqueness is checked against the database.</summary>
    public static bool IsValid(string? nickname) => nickname is not null && Pattern().IsMatch(nickname.Trim());

    /// <summary>The trimmed nickname, or 400 <c>nickname.invalid</c> on the <c>nickname</c> field.</summary>
    public static string Validate(string? nickname)
    {
        if (nickname is null || !IsValid(nickname))
        {
            throw new DomainValidationException(
                ErrorCodes.NicknameInvalid,
                new Dictionary<string, string[]> { ["nickname"] = [ErrorCodes.NicknameInvalid] });
        }

        return nickname.Trim();
    }

    /// <summary>The form uniqueness is checked on (<see cref="NameNormalizer"/>: lower-cased, diacritics removed).</summary>
    public static string Normalize(string nickname) => NameNormalizer.Normalize(nickname);

    // \p{L} covers ą ć ę ł ń ó ś ź ż and their capitals, \p{Nd} decimal digits. No spaces or other symbols.
    [GeneratedRegex(@"^[\p{L}\p{Nd}_-]{3,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
