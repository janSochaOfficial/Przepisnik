using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Fridges;

/// <summary>Items of a fridge (API.md §6, PROJECT §8.2). Every member has full edit rights.</summary>
public interface IFridgeItemService
{
    /// <summary>The fridge's items by product category, product name, then most-used first. Two statements.</summary>
    Task<IReadOnlyList<FridgeItemModel>> ListAsync(Guid fridgeId, CancellationToken ct);

    /// <summary>Adds a package (remaining percent &gt; 0).</summary>
    Task<FridgeItemModel> CreateAsync(Guid fridgeId, FridgeItemInput input, CancellationToken ct);

    /// <summary>Edits an item; returns null when <c>remainingPercent</c> is 0 and the item was removed.</summary>
    Task<FridgeItemModel?> UpdateAsync(Guid fridgeId, Guid itemId, FridgeItemInput input, uint version, CancellationToken ct);

    /// <summary>Removes an item.</summary>
    Task DeleteAsync(Guid fridgeId, Guid itemId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class FridgeItemService(IAppDbContext db, ICurrentUser currentUser, FridgeAccess access, TimeProvider time)
    : IFridgeItemService
{
    public const int MaxNoteLength = 300;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FridgeItemModel>> ListAsync(Guid fridgeId, CancellationToken ct)
    {
        await access.RequireMemberAsync(fridgeId, ct);
        var items = await FridgeItemQueries.Project(db, db.FridgeItems.Where(i => i.FridgeId == fridgeId)).ToListAsync(ct);
        return FridgeItemQueries.Order(items);
    }

    /// <inheritdoc />
    public async Task<FridgeItemModel> CreateAsync(Guid fridgeId, FridgeItemInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        await access.RequireMemberAsync(fridgeId, ct);
        var percent = await ValidateAsync(input, allowZero: false, ct);

        var now = time.GetUtcNow();
        var item = new FridgeItem
        {
            FridgeId = fridgeId,
            ProductId = input.ProductId,
            PackageAmount = input.PackageAmount,
            RemainingPercent = percent,
            Note = NormalizeNote(input.Note),
            AddedById = currentUser.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.FridgeItems.Add(item);
        await db.SaveChangesAsync(ct);
        return await GetAsync(item.Id, ct);
    }

    /// <inheritdoc />
    public async Task<FridgeItemModel?> UpdateAsync(Guid fridgeId, Guid itemId, FridgeItemInput input, uint version, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        await access.RequireMemberAsync(fridgeId, ct);
        var item = await db.FridgeItems.SingleOrDefaultAsync(i => i.Id == itemId && i.FridgeId == fridgeId, ct)
            ?? throw new NotFoundException(ErrorCodes.FridgeItemNotFound);
        var percent = await ValidateAsync(input, allowZero: true, ct);

        if (item.Version != version)
        {
            throw new ConflictException(ErrorCodes.ConcurrencyConflict, "The item was changed by someone else.");
        }

        if (percent == 0m)
        {
            db.FridgeItems.Remove(item);
            await db.SaveChangesAsync(ct);
            return null;
        }

        item.ProductId = input.ProductId;
        item.PackageAmount = input.PackageAmount;
        item.RemainingPercent = percent;
        item.Note = NormalizeNote(input.Note);
        item.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return await GetAsync(item.Id, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid fridgeId, Guid itemId, CancellationToken ct)
    {
        await access.RequireMemberAsync(fridgeId, ct);
        var item = await db.FridgeItems.SingleOrDefaultAsync(i => i.Id == itemId && i.FridgeId == fridgeId, ct)
            ?? throw new NotFoundException(ErrorCodes.FridgeItemNotFound);
        db.FridgeItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    private async Task<FridgeItemModel> GetAsync(Guid itemId, CancellationToken ct) =>
        await FridgeItemQueries.Project(db, db.FridgeItems.Where(i => i.Id == itemId)).SingleAsync(ct);

    /// <summary>Validates the write shape and returns the percent rounded to 2 decimals.</summary>
    private async Task<decimal> ValidateAsync(FridgeItemInput input, bool allowZero, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        if (input.PackageAmount <= 0)
        {
            errors.Add("packageAmount", "out_of_range");
        }

        var percent = Math.Round(input.RemainingPercent, DeductionAllocator.PercentDecimals, MidpointRounding.AwayFromZero);
        if (percent is < 0m or > 100m || (!allowZero && percent == 0m))
        {
            errors.Add("remainingPercent", "out_of_range");
        }

        if (input.Note is { Length: > MaxNoteLength })
        {
            errors.Add("note", "too_long");
        }

        if (!await db.Products.AnyAsync(p => p.Id == input.ProductId, ct))
        {
            errors.Add("productId", ErrorCodes.ProductNotFound);
        }

        errors.ThrowIfAny();
        return percent;
    }

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}

/// <summary>The one projection of fridge items (with product and adder) shared by items, deductions and shopping.</summary>
public static class FridgeItemQueries
{
    /// <summary>Projects items to <see cref="FridgeItemModel"/> in one statement (product joined, nickname as a subquery).</summary>
    public static IQueryable<FridgeItemModel> Project(IAppDbContext db, IQueryable<FridgeItem> items)
    {
        ArgumentNullException.ThrowIfNull(db);
        var users = db.Users;
        return items
            .AsNoTracking()
            .Select(i => new FridgeItemModel(
                i,
                i.Product!,
                new FridgeUserRef(i.AddedById, users.Where(u => u.Id == i.AddedById).Select(u => u.Nickname).FirstOrDefault() ?? "")));
    }

    /// <summary>By category (catalog order), product name, most-used package first, then oldest first.</summary>
    public static IReadOnlyList<FridgeItemModel> Order(IEnumerable<FridgeItemModel> items) =>
        [.. items
            .OrderBy(m => m.Product.Category)
            .ThenBy(m => m.Product.NormalizedName, StringComparer.Ordinal)
            .ThenBy(m => m.Item.RemainingPercent)
            .ThenBy(m => m.Item.CreatedAt)
            .ThenBy(m => m.Item.Id)];
}
