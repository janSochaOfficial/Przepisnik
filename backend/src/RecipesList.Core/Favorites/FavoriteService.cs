using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Favorites;

/// <summary>The kind of a favourite entry.</summary>
public enum FavoriteKind
{
    Recipe,
    Variant,
}

/// <summary>One favourite in the merged list: the item id (recipe or variant) and when it was favourited.</summary>
public sealed record FavoriteKey(FavoriteKind Kind, Guid ItemId, DateTimeOffset FavoritedAt);

/// <summary>Marking, unmarking and paging ♥ (PROJECT §4.6).</summary>
public interface IFavoriteService
{
    /// <summary>Idempotent. Throws <see cref="NotFoundException"/> when the recipe is not visible to the caller.</summary>
    Task FavoriteRecipeAsync(Guid recipeId, CancellationToken ct);

    /// <summary>Idempotent; never fails for a missing or invisible recipe.</summary>
    Task UnfavoriteRecipeAsync(Guid recipeId, CancellationToken ct);

    /// <summary>Idempotent. Throws <see cref="NotFoundException"/> when the variant is not visible to the caller.</summary>
    Task FavoriteVariantAsync(Guid variantId, CancellationToken ct);

    /// <summary>Idempotent; never fails for a missing or invisible variant.</summary>
    Task UnfavoriteVariantAsync(Guid variantId, CancellationToken ct);

    /// <summary>The caller's currently visible favourites, newest first, paged in SQL.</summary>
    Task<Page<FavoriteKey>> ListAsync(int page, int pageSize, CancellationToken ct);
}

/// <inheritdoc />
public sealed class FavoriteService(IAppDbContext db, VisibilityRules visibility, ICurrentUser currentUser, TimeProvider clock) : IFavoriteService
{
    /// <inheritdoc />
    public async Task FavoriteRecipeAsync(Guid recipeId, CancellationToken ct)
    {
        var me = currentUser.Id;
        if (!await visibility.VisibleRecipes(me).AnyAsync(r => r.Id == recipeId, ct))
        {
            throw new NotFoundException(ErrorCodes.RecipeNotFound);
        }

        if (await db.RecipeFavorites.AnyAsync(f => f.UserId == me && f.RecipeId == recipeId, ct))
        {
            return;
        }

        db.RecipeFavorites.Add(new RecipeFavorite { UserId = me, RecipeId = recipeId, CreatedAt = clock.GetUtcNow() });
        await SaveIgnoringDuplicateAsync(ct);
    }

    /// <inheritdoc />
    public async Task UnfavoriteRecipeAsync(Guid recipeId, CancellationToken ct)
    {
        var me = currentUser.Id;
        await db.RecipeFavorites.Where(f => f.UserId == me && f.RecipeId == recipeId).ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task FavoriteVariantAsync(Guid variantId, CancellationToken ct)
    {
        var me = currentUser.Id;
        if (!await visibility.VisibleVariants(me).AnyAsync(v => v.Id == variantId, ct))
        {
            throw new NotFoundException(ErrorCodes.VariantNotFound);
        }

        if (await db.VariantFavorites.AnyAsync(f => f.UserId == me && f.VariantId == variantId, ct))
        {
            return;
        }

        db.VariantFavorites.Add(new VariantFavorite { UserId = me, VariantId = variantId, CreatedAt = clock.GetUtcNow() });
        await SaveIgnoringDuplicateAsync(ct);
    }

    /// <inheritdoc />
    public async Task UnfavoriteVariantAsync(Guid variantId, CancellationToken ct)
    {
        var me = currentUser.Id;
        await db.VariantFavorites.Where(f => f.UserId == me && f.VariantId == variantId).ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Page<FavoriteKey>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var me = currentUser.Id;
        var recipes = visibility.VisibleRecipes(me);

        // Hidden items are filtered, not deleted (PROJECT §4.6). A variant entry also needs its base recipe visible,
        // because the entry carries the recipe summary.
        var recipeKeys = db.RecipeFavorites
            .Where(f => f.UserId == me && recipes.Any(r => r.Id == f.RecipeId && !r.IsArchived))
            .Select(f => new KeyRow { IsVariant = false, ItemId = f.RecipeId, At = f.CreatedAt });
        var visibleVariants = visibility.VisibleVariants(me);
        var variantKeys = db.VariantFavorites
            .Where(f => f.UserId == me
                && visibleVariants.Any(v => v.Id == f.VariantId && recipes.Any(r => r.Id == v.RecipeId)))
            .Select(f => new KeyRow { IsVariant = true, ItemId = f.VariantId, At = f.CreatedAt });

        var all = recipeKeys.Concat(variantKeys);
        var total = await all.CountAsync(ct);
        var rows = await all
            .OrderByDescending(k => k.At).ThenByDescending(k => k.ItemId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return new Page<FavoriteKey>(
            [.. rows.Select(r => new FavoriteKey(r.IsVariant ? FavoriteKind.Variant : FavoriteKind.Recipe, r.ItemId, r.At))],
            page,
            pageSize,
            total);
    }

    private async Task SaveIgnoringDuplicateAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request inserted the same favourite first; PUT is idempotent.
        }
    }

    private sealed class KeyRow
    {
        public bool IsVariant { get; init; }

        public Guid ItemId { get; init; }

        public DateTimeOffset At { get; init; }
    }
}
