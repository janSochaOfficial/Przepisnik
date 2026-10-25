using System.Text;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Variants;

/// <summary>
/// Aligns an imported recipe with an existing one, producing the edited full copy of the existing recipe that the
/// variant diff turns into operations (PROJECT §4.7, D-35). Pure: no I/O.
/// </summary>
/// <remarks>
/// <para><b>Ingredients:</b> a longest common subsequence on product id. Matched rows keep the target row's id and
/// take the source's quantity, note and optional flag; unmatched target rows are removed; unmatched source rows are
/// added where they stand in the source, i.e. after the nearest preceding matched (or added) row.</para>
/// <para><b>Steps</b>, per section: a longest common subsequence on the normalized text of the step (text blocks
/// joined, trimmed, whitespace collapsed). Between two anchors (the start and the end of the section count as anchors)
/// the unmatched steps are paired by position as replacements; the rest are removed (target) or added (source).</para>
/// <para><b>Utensils:</b> the source's set. <b>Times:</b> the source's, except that a time the source leaves empty
/// keeps the target's (a variant cannot clear a base time). <b>Servings</b> likewise: the source's, or the target's when the source has none. Tags and the cover are not part of variants.</para>
/// <para>Ties of the subsequence keep the earliest target row. The order of target rows is never changed (D-20):
/// the copy lists rows in source order, and matched target rows appear in it in their own order.</para>
/// </remarks>
public static class ImportAlignment
{
    /// <summary>The edited copy of <paramref name="target"/> that has the content of <paramref name="source"/>.</summary>
    public static RecipeDraft Align(ResolvedRecipeModel target, ResolvedRecipeModel source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        return new RecipeDraft(
            AlignIngredients(target.Ingredients, source.Ingredients),
            AlignSteps(target.PrepSteps, source.PrepSteps),
            AlignSteps(target.InstructionSteps, source.InstructionSteps),
            [.. source.UtensilIds.Distinct()],
            source.PrepMinutes ?? target.PrepMinutes,
            source.CookMinutes ?? target.CookMinutes,
            source.Servings ?? target.Servings);
    }

    /// <summary>
    /// Whether two resolved recipes have the same content, ignoring row ids: the same ingredients (product, quantity,
    /// note, optional) and steps in the same order, the same set of utensils, the same times and the same servings.
    /// </summary>
    public static bool HasSameContent(ResolvedRecipeModel a, ResolvedRecipeModel b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a.PrepMinutes == b.PrepMinutes
            && a.CookMinutes == b.CookMinutes
            && a.Servings == b.Servings
            && a.UtensilIds.ToHashSet().SetEquals(b.UtensilIds)
            && a.Ingredients.Count == b.Ingredients.Count
            && a.Ingredients.Zip(b.Ingredients).All(p => SameIngredient(p.First, p.Second))
            && SameSteps(a.PrepSteps, b.PrepSteps)
            && SameSteps(a.InstructionSteps, b.InstructionSteps);
    }

    /// <summary>The text a step is aligned by: its text blocks joined by a space, trimmed, whitespace collapsed.</summary>
    public static string StepText(IReadOnlyList<ContentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var builder = new StringBuilder();
        var pendingSpace = false;
        foreach (var text in blocks.OfType<TextBlock>().Select(b => b.Text))
        {
            pendingSpace = builder.Length > 0;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static List<IngredientDraft> AlignIngredients(IReadOnlyList<ResolvedIngredient> target, IReadOnlyList<ResolvedIngredient> source)
    {
        var pairs = Lcs(target.Select(r => r.ProductId).ToList(), source.Select(r => r.ProductId).ToList());
        var matched = pairs.ToDictionary(p => p.Source, p => p.Target);
        return [.. source.Select((row, j) => new IngredientDraft(
            matched.TryGetValue(j, out var t) ? target[t].Id : null,
            row.ProductId,
            row.Quantity,
            row.BaseAmount,
            row.Note,
            row.Optional))];
    }

    private static List<StepDraft> AlignSteps(IReadOnlyList<ResolvedStep> target, IReadOnlyList<ResolvedStep> source)
    {
        var anchors = Lcs(target.Select(s => StepText(s.Blocks)).ToList(), source.Select(s => StepText(s.Blocks)).ToList());

        // Each source step gets the target id it keeps (anchor or positional replacement), or none (added).
        var ids = new Guid?[source.Count];
        var previousTarget = 0;
        var previousSource = 0;
        foreach (var (t, s) in anchors.Append((target.Count, source.Count)))
        {
            var gap = Math.Min(t - previousTarget, s - previousSource);
            for (var k = 0; k < gap; k++)
            {
                ids[previousSource + k] = target[previousTarget + k].Id;
            }

            if (s < source.Count)
            {
                ids[s] = target[t].Id;
            }

            previousTarget = t + 1;
            previousSource = s + 1;
        }

        return [.. source.Select((step, j) => new StepDraft(ids[j], step.Blocks))];
    }

    // The matched (target index, source index) pairs of a longest common subsequence, in order. On a tie the earliest
    // target row stays available for matching, so the source row is skipped (added) rather than the target row.
    private static List<(int Target, int Source)> Lcs<T>(List<T> target, List<T> source)
    {
        var comparer = EqualityComparer<T>.Default;
        var n = target.Count;
        var m = source.Count;
        var length = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                length[i, j] = comparer.Equals(target[i], source[j])
                    ? length[i + 1, j + 1] + 1
                    : Math.Max(length[i + 1, j], length[i, j + 1]);
            }
        }

        var pairs = new List<(int, int)>();
        var (a, b) = (0, 0);
        while (a < n && b < m)
        {
            if (comparer.Equals(target[a], source[b]))
            {
                pairs.Add((a, b));
                a++;
                b++;
            }
            else if (length[a, b + 1] >= length[a + 1, b])
            {
                b++;
            }
            else
            {
                a++;
            }
        }

        return pairs;
    }

    private static bool SameIngredient(ResolvedIngredient a, ResolvedIngredient b) =>
        a.ProductId == b.ProductId
        && Equals(a.Quantity, b.Quantity)
        && NormalizeNote(a.Note) == NormalizeNote(b.Note)
        && a.Optional == b.Optional;

    private static bool SameSteps(IReadOnlyList<ResolvedStep> a, IReadOnlyList<ResolvedStep> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Blocks.SequenceEqual(p.Second.Blocks));

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note;
}
