using Microsoft.EntityFrameworkCore;
using RecipesList.Api.Features.Images;
using RecipesList.Core.Common;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Recipes;

/// <summary>Filters of <c>GET /recipes</c> (API.md §4), already parsed.</summary>
internal sealed record RecipeListQuery(
    string? Q,
    IReadOnlyList<Guid> TagIds,
    string? Author,
    VisibilityLevel? Visibility,
    int Page,
    int PageSize,
    string Sort);

/// <summary>
/// Read side of recipes. Every query starts from <see cref="VisibilityRules.VisibleRecipes"/>, so a recipe the caller
/// cannot see is simply absent (404). List queries run a constant number of statements per page.
/// </summary>
internal sealed class RecipeQueries(IAppDbContext db, VisibilityRules visibility, ICurrentUser currentUser)
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public async Task<PagedDto<RecipeSummaryDto>> ListAsync(RecipeListQuery query, CancellationToken ct)
    {
        var me = currentUser.Id;
        var recipes = visibility.VisibleRecipes(me).Where(r => !r.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // Normalized (lower-case, no diacritics) on both sides; LIKE '%…%' is served by the trigram index.
            var pattern = "%" + EscapeLike(NameNormalizer.Normalize(query.Q)) + "%";
            recipes = recipes.Where(r => EF.Functions.Like(r.NormalizedTitle, pattern, "\\"));
        }

        if (query.TagIds.Count > 0)
        {
            var tagIds = query.TagIds.Distinct().ToList();
            recipes = recipes.Where(r => r.Tags.Count(t => tagIds.Contains(t.TagId)) == tagIds.Count);
        }

        if (query.Author is { } author)
        {
            var authorId = author == "me" ? me : Guid.Parse(author);
            recipes = recipes.Where(r => r.AuthorId == authorId);
        }

        if (query.Visibility is { } level)
        {
            recipes = recipes.Where(r => r.Visibility == level);
        }

        var ordered = query.Sort == "title"
            ? recipes.OrderBy(r => r.NormalizedTitle).ThenBy(r => r.Id)
            : recipes.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        var total = await recipes.CountAsync(ct);
        var variants = visibility.VisibleVariants(me);
        var favorites = db.RecipeFavorites;

        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new
            {
                r.Id,
                r.Title,
                AuthorId = r.AuthorId,
                Nickname = r.Author!.Nickname,
                CoverId = r.CoverImageId,
                CoverWidth = r.CoverImage != null ? r.CoverImage.Width : 0,
                CoverHeight = r.CoverImage != null ? r.CoverImage.Height : 0,
                r.Visibility,
                IsFavorite = favorites.Any(f => f.UserId == me && f.RecipeId == r.Id),
                Tags = r.Tags.Select(t => new TagRefDto(t.Tag!.Id, t.Tag.Name, t.Tag.IsSystem)).ToList(),
                r.PrepMinutes,
                r.CookMinutes,
                VariantCount = variants.Count(v => v.RecipeId == r.Id),
                r.CreatedAt,
                r.UpdatedAt,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var items = rows.Select(r => new RecipeSummaryDto(
            r.Id,
            r.Title,
            new UserRefDto(r.AuthorId, r.Nickname ?? ""),
            r.CoverId is { } cover ? ImageInfoDto.From(cover, r.CoverWidth, r.CoverHeight) : null,
            r.Visibility,
            r.IsFavorite,
            [.. r.Tags.OrderBy(t => t.Name, StringComparer.Ordinal)],
            r.PrepMinutes,
            r.CookMinutes,
            r.VariantCount,
            r.CreatedAt,
            r.UpdatedAt)).ToList();

        return new PagedDto<RecipeSummaryDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>The recipe as the caller may see it, or null (not found or not visible). Archived ones are included.</summary>
    public async Task<RecipeDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var me = currentUser.Id;
        var recipe = await visibility.VisibleRecipes(me)
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.CoverImage)
            .Include(r => r.Ingredients).ThenInclude(i => i.Product)
            .Include(r => r.Steps)
            .Include(r => r.Tags).ThenInclude(t => t.Tag)
            .Include(r => r.Utensils).ThenInclude(u => u.Utensil)
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == id, ct);
        if (recipe is null)
        {
            return null;
        }

        var isAuthor = recipe.AuthorId == me;
        var visibleVariants = await visibility.VisibleVariants(me).CountAsync(v => v.RecipeId == id, ct);
        var isFavorite = await db.RecipeFavorites.AnyAsync(f => f.UserId == me && f.RecipeId == id, ct);

        var total = 0;
        var foreign = 0;
        if (isAuthor)
        {
            var counts = await db.Variants
                .Where(v => v.RecipeId == id)
                .GroupBy(_ => 1)
                .Select(g => new { Total = g.Count(), Foreign = g.Count(v => v.AuthorId != me) })
                .SingleOrDefaultAsync(ct);
            total = counts?.Total ?? 0;
            foreign = counts?.Foreign ?? 0;
        }

        return new RecipeDetailDto(
            recipe.Id,
            recipe.Version,
            recipe.IsArchived,
            recipe.Visibility,
            isFavorite,
            CanEdit: isAuthor && recipe.Visibility != VisibilityLevel.Public,
            total,
            foreign,
            recipe.Title,
            recipe.Description,
            recipe.CoverImage is { } cover ? ImageInfoDto.From(cover.Id, cover.Width, cover.Height) : null,
            new UserRefDto(recipe.AuthorId, recipe.Author?.Nickname ?? ""),
            recipe.PrepMinutes,
            recipe.CookMinutes,
            [.. recipe.Tags.Select(t => new TagRefDto(t.Tag!.Id, t.Tag.Name, t.Tag.IsSystem)).OrderBy(t => t.Name, StringComparer.Ordinal)],
            [.. recipe.Utensils.Select(u => new UtensilRefDto(u.Utensil!.Id, u.Utensil.Name)).OrderBy(u => u.Name, StringComparer.Ordinal)],
            [.. recipe.Ingredients.OrderBy(i => i.Position).Select(i => new IngredientDto(
                i.Id, RecipeProductRefDto.From(i.Product!), i.Quantity, i.BaseAmount, i.Note, i.Optional))],
            Steps(recipe, StepSection.Prep),
            Steps(recipe, StepSection.Instructions),
            recipe.CookCount,
            visibleVariants,
            recipe.CreatedAt,
            recipe.UpdatedAt);
    }

    private static List<StepDto> Steps(Recipe recipe, StepSection section) =>
        [.. recipe.Steps.Where(s => s.Section == section).OrderBy(s => s.Position).Select(s => new StepDto(s.Id, s.Blocks))];

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
