using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;

namespace RecipesList.Api.Features.Catalog;

/// <summary>The outcome of an idempotent create: the entity and whether this call created it.</summary>
internal readonly record struct CreateResult<T>(T Entity, bool Created);

/// <summary>Search and idempotent create for tags and utensils (API.md §2, PROJECT §4.3).</summary>
internal sealed class NameCatalogs(IAppDbContext db)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public async Task<List<Tag>> SearchTagsAsync(string? q, int? limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var tags = db.Tags.AsNoTracking();
        var term = NameNormalizer.Normalize(q ?? "");
        if (term.Length == 0)
        {
            return await tags.OrderBy(t => t.NormalizedName).Take(take).ToListAsync(ct);
        }

        var escaped = NameSearch.EscapeLike(term);
        var prefix = escaped + "%";
        var wordPrefix = "% " + escaped + "%";
        return await tags
            .Where(t => EF.Functions.Like(t.NormalizedName, prefix, NameSearch.LikeEscape)
                || EF.Functions.Like(t.NormalizedName, wordPrefix, NameSearch.LikeEscape)
                || EF.Functions.TrigramsSimilarity(t.NormalizedName, term) > ProductCatalog.SimilarityThreshold)
            .OrderBy(t => EF.Functions.Like(t.NormalizedName, prefix, NameSearch.LikeEscape) ? 0
                : EF.Functions.Like(t.NormalizedName, wordPrefix, NameSearch.LikeEscape) ? 1 : 2)
            .ThenByDescending(t => EF.Functions.TrigramsSimilarity(t.NormalizedName, term))
            .ThenBy(t => t.NormalizedName)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<Utensil>> SearchUtensilsAsync(string? q, int? limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var utensils = db.Utensils.AsNoTracking();
        var term = NameNormalizer.Normalize(q ?? "");
        if (term.Length == 0)
        {
            return await utensils.OrderBy(u => u.NormalizedName).Take(take).ToListAsync(ct);
        }

        var escaped = NameSearch.EscapeLike(term);
        var prefix = escaped + "%";
        var wordPrefix = "% " + escaped + "%";
        return await utensils
            .Where(u => EF.Functions.Like(u.NormalizedName, prefix, NameSearch.LikeEscape)
                || EF.Functions.Like(u.NormalizedName, wordPrefix, NameSearch.LikeEscape)
                || EF.Functions.TrigramsSimilarity(u.NormalizedName, term) > ProductCatalog.SimilarityThreshold)
            .OrderBy(u => EF.Functions.Like(u.NormalizedName, prefix, NameSearch.LikeEscape) ? 0
                : EF.Functions.Like(u.NormalizedName, wordPrefix, NameSearch.LikeEscape) ? 1 : 2)
            .ThenByDescending(u => EF.Functions.TrigramsSimilarity(u.NormalizedName, term))
            .ThenBy(u => u.NormalizedName)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Creates a tag, or returns the one with the same normalized name. <c>szybkie</c> is reserved.</summary>
    public async Task<CreateResult<Tag>> CreateTagAsync(string? name, CancellationToken ct)
    {
        var clean = ProductRules.ValidateSimpleName(name);
        var normalized = NameNormalizer.Normalize(clean);
        if (normalized == NameNormalizer.Normalize(Tag.QuickTagName))
        {
            throw new DomainValidationException(ErrorCodes.TagReserved);
        }

        if (await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalized, ct) is { } existing)
        {
            return new(existing, false);
        }

        var tag = new Tag { Name = clean, NormalizedName = normalized, IsSystem = false };
        db.Tags.Add(tag);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request created the same name: return that one.
            return new(
                await db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.NormalizedName == normalized, ct) ?? throw new InvalidOperationException("Tag insert failed."),
                false);
        }

        return new(tag, true);
    }

    /// <summary>Creates a utensil, or returns the one with the same normalized name.</summary>
    public async Task<CreateResult<Utensil>> CreateUtensilAsync(string? name, CancellationToken ct)
    {
        var clean = ProductRules.ValidateSimpleName(name);
        var normalized = NameNormalizer.Normalize(clean);

        if (await db.Utensils.FirstOrDefaultAsync(u => u.NormalizedName == normalized, ct) is { } existing)
        {
            return new(existing, false);
        }

        var utensil = new Utensil { Name = clean, NormalizedName = normalized };
        db.Utensils.Add(utensil);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new(
                await db.Utensils.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedName == normalized, ct) ?? throw new InvalidOperationException("Utensil insert failed."),
                false);
        }

        return new(utensil, true);
    }
}
