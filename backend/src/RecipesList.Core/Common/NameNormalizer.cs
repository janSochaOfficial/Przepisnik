using System.Globalization;
using System.Text;

namespace RecipesList.Core.Common;

/// <summary>
/// Normalizes catalog names, nicknames and titles for uniqueness and search (PROJECT §7.1):
/// trimmed, inner whitespace collapsed to one space, lower-case, diacritics removed (including <c>ł</c> → <c>l</c>).
/// </summary>
public static class NameNormalizer
{
    /// <summary>Returns the normalized form of <paramref name="s"/>; <c>"  Śmietana   18% "</c> → <c>"smietana 18%"</c>.</summary>
    public static string Normalize(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var decomposed = s.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var c in decomposed)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(Fold(char.ToLowerInvariant(c)));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    // Letters whose diacritic is not a combining mark, so FormD does not split them.
    private static char Fold(char c) => c switch
    {
        'ł' => 'l',
        'ø' => 'o',
        'đ' => 'd',
        _ => c,
    };
}
