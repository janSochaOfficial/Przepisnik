namespace RecipesList.Core.Transfer;

/// <summary>
/// The "originally by" note that import appends to the description of the recipe and of each variant (PROJECT §4.7).
/// Export strips it again and restores the nickname, so <c>export(import(export(R)))</c> equals <c>export(R)</c>.
/// </summary>
public static class ImportFooter
{
    /// <summary>The footer text up to the nickname; the nickname follows without a space.</summary>
    public const string Prefix = "Zaimportowano — oryginalnie: @";

    private const string Separator = "\n\n";

    /// <summary>
    /// Appends the footer. When the result would exceed <paramref name="maxLength"/>, the original text is cut and ends with "…".
    /// </summary>
    public static string Append(string? description, string authorNickname, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(authorNickname);
        var footer = Prefix + authorNickname;
        var text = description?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return footer;
        }

        var room = maxLength - footer.Length - Separator.Length;
        if (room < 1)
        {
            return footer;
        }

        if (text.Length > room)
        {
            text = text[..(room - 1)].TrimEnd() + "…";
        }

        return text + Separator + footer;
    }

    /// <summary>Splits a trailing footer off a description: the remaining text (null when empty) and the nickname, if any.</summary>
    public static (string? Text, string? Nickname) Strip(string? description)
    {
        if (description is null)
        {
            return (null, null);
        }

        var at = description.LastIndexOf(Prefix, StringComparison.Ordinal);
        if (at < 0)
        {
            return (description, null);
        }

        var nickname = description[(at + Prefix.Length)..];
        var atStart = at == 0;
        var afterSeparator = at >= Separator.Length && description.AsSpan(at - Separator.Length, Separator.Length).SequenceEqual(Separator);
        if (nickname.Length == 0 || nickname.Any(char.IsWhiteSpace) || !(atStart || afterSeparator))
        {
            return (description, null);
        }

        var text = atStart ? "" : description[..(at - Separator.Length)];
        return (text.Length == 0 ? null : text, nickname);
    }
}
