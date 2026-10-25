using System.Transactions;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Recipes;

/// <summary>What <see cref="ICookService.TryCountAsync"/> decided.</summary>
/// <param name="Counted">True when this cook went into the counts.</param>
/// <param name="NextAllowedAt">When the caller's next cook of the recipe counts: 12 hours after the counted one.</param>
public sealed record CookCountOutcome(bool Counted, DateTimeOffset NextAllowedAt);

/// <summary>The counts after a recorded "Ugotowane!" (API.md §4 <c>CookResult</c>).</summary>
public sealed record CookResult(int RecipeCookCount, int? VariantCookCount, DateTimeOffset NextAllowedAt);

/// <summary>Cook counts and the 12-hour rule (PROJECT §9.1, D-42, D-43).</summary>
public interface ICookService
{
    /// <summary>The rule shared by deductions and "Ugotowane!": a user's cooks of one recipe count at most once per this long.</summary>
    static readonly TimeSpan Window = TimeSpan.FromHours(12);

    /// <summary>The isolation level every transaction that counts a cook runs at; an outer scope must use it too.</summary>
    static readonly TransactionOptions ScopeOptions = new() { IsolationLevel = IsolationLevel.ReadCommitted };

    /// <summary>
    /// Decides in one atomic statement whether this cook counts (so simultaneous requests cannot both count) and, when it
    /// does, adds it: the recipe's total, plus the variant's count or the base's count when there is no variant. Joins the
    /// caller's transaction. Does not check visibility; the caller has.
    /// </summary>
    Task<CookCountOutcome> TryCountAsync(Guid userId, Guid recipeId, Guid? variantId, CancellationToken ct);

    /// <summary>
    /// "Ugotowane!": records a cook without a fridge. 404 <c>recipe.not_found</c> / <c>variant.not_found</c>, 409
    /// <c>recipe.archived</c>, 409 <c>cook.too_soon</c> (extension <c>nextAllowedAt</c>) inside the 12 hours.
    /// </summary>
    Task<CookResult> CookAsync(Guid recipeId, Guid? variantId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class CookService(IAppDbContext db, ICurrentUser currentUser, VisibilityRules visibility, TimeProvider time) : ICookService
{
    /// <inheritdoc />
    public async Task<CookCountOutcome> TryCountAsync(Guid userId, Guid recipeId, Guid? variantId, CancellationToken ct)
    {
        // PostgreSQL keeps microseconds; truncate so the value returned now equals the one read back later.
        var utcNow = time.GetUtcNow();
        var now = new DateTimeOffset(utcNow.UtcTicks - (utcNow.UtcTicks % 10), TimeSpan.Zero);
        // Read committed, not TransactionScope's default serializable: there a concurrent ON CONFLICT DO UPDATE fails with
        // a serialization error (500) instead of re-checking the WHERE against the committed row.
        using var scope = new TransactionScope(TransactionScopeOption.Required, ICookService.ScopeOptions, TransactionScopeAsyncFlowOption.Enabled);
        var counted = await db.TryClaimCookAsync(userId, recipeId, now, now - ICookService.Window, ct);
        if (counted)
        {
            await db.IncrementCookCountsAsync(recipeId, variantId, ct);
            scope.Complete();
            return new CookCountOutcome(true, now + ICookService.Window);
        }

        // Read before completing: a completed scope no longer allows a connection to be opened in it.
        var last = await db.UserRecipeCooks.AsNoTracking()
            .Where(c => c.UserId == userId && c.RecipeId == recipeId)
            .Select(c => c.LastCountedAt)
            .SingleAsync(ct);
        scope.Complete();
        return new CookCountOutcome(false, last + ICookService.Window);
    }

    /// <inheritdoc />
    public async Task<CookResult> CookAsync(Guid recipeId, Guid? variantId, CancellationToken ct)
    {
        var me = currentUser.Id;
        var archived = await visibility.VisibleRecipes(me)
            .Where(r => r.Id == recipeId)
            .Select(r => (bool?)r.IsArchived)
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        if (variantId is { } id
            && !await visibility.VisibleVariants(me).AnyAsync(v => v.Id == id && v.RecipeId == recipeId, ct))
        {
            throw new NotFoundException(ErrorCodes.VariantNotFound);
        }

        if (archived)
        {
            throw new ConflictException(ErrorCodes.RecipeArchived, "An archived recipe is read-only.");
        }

        var outcome = await TryCountAsync(me, recipeId, variantId, ct);
        if (!outcome.Counted)
        {
            throw new ConflictException(ErrorCodes.CookTooSoon, "This recipe was already counted as cooked in the last 12 hours.")
                .With("nextAllowedAt", outcome.NextAllowedAt);
        }

        var recipeCount = await db.RecipeCookCounts.AsNoTracking().Where(c => c.RecipeId == recipeId).Select(c => c.Count).SingleAsync(ct);
        int? variantCount = variantId is { } v
            ? await db.VariantCookCounts.AsNoTracking().Where(c => c.VariantId == v).Select(c => c.Count).SingleAsync(ct)
            : null;
        return new CookResult(recipeCount, variantCount, outcome.NextAllowedAt);
    }
}
