using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Fridges;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.ShoppingLists;

/// <summary>A shopping-list entry with its product (null for free text) and the user who added it.</summary>
public sealed record ShoppingListItemModel(ShoppingListItem Item, Product? Product, FridgeUserRef AddedBy);

/// <summary>One entry to add: a product (with an optional amount) or free text (API.md §7 <c>ShoppingListItemWrite</c>).</summary>
public sealed record ShoppingListItemInput(Guid? ProductId, string? Text, decimal? Amount);

/// <summary>A partial edit; null fields are left unchanged (API.md §7 <c>ShoppingListItemPatch</c>).</summary>
public sealed record ShoppingListItemPatchInput(uint Version, bool? Checked, decimal? Amount, string? Text);

/// <summary>What <c>complete</c> did: how many checked entries were removed and the fridge items created from them.</summary>
public sealed record CompleteShoppingListModel(int Removed, IReadOnlyList<FridgeItemModel> CreatedFridgeItems);

/// <summary>The per-fridge shopping list (API.md §7, PROJECT §10, D-11). Every fridge member has full edit rights.</summary>
public interface IShoppingListService
{
    /// <summary>Unchecked first, then by category (catalog order; free text last), then by name. Two statements.</summary>
    Task<IReadOnlyList<ShoppingListItemModel>> ListAsync(Guid fridgeId, CancellationToken ct);

    /// <summary>
    /// Adds entries. A product that already has an unchecked entry (or appears earlier in the batch) is merged into it
    /// by summing the amounts (null + x = x). Returns the created or merged entries, one per distinct target, in
    /// request order.
    /// </summary>
    Task<IReadOnlyList<ShoppingListItemModel>> AddAsync(Guid fridgeId, IReadOnlyList<ShoppingListItemInput> items, CancellationToken ct);

    /// <summary>Checks off or edits an entry (optimistic concurrency on <c>version</c>).</summary>
    Task<ShoppingListItemModel> PatchAsync(Guid fridgeId, Guid itemId, ShoppingListItemPatchInput patch, CancellationToken ct);

    /// <summary>Removes an entry.</summary>
    Task DeleteAsync(Guid fridgeId, Guid itemId, CancellationToken ct);

    /// <summary>
    /// Removes the checked entries. With <paramref name="moveToFridge"/>, checked product entries first become fridge
    /// items at 100 % (package = the amount, or the product's default package); a product entry with neither stays on
    /// the list. Free-text entries are just removed. The created items come back in shopping-list order.
    /// </summary>
    Task<CompleteShoppingListModel> CompleteAsync(Guid fridgeId, bool moveToFridge, CancellationToken ct);
}

/// <inheritdoc />
public sealed class ShoppingListService(IAppDbContext db, ICurrentUser currentUser, FridgeAccess access, TimeProvider time)
    : IShoppingListService
{
    public const int MaxTextLength = 200;
    public const int MaxBatch = 100;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShoppingListItemModel>> ListAsync(Guid fridgeId, CancellationToken ct)
    {
        await access.RequireMemberAsync(fridgeId, ct);
        var items = await Project(db.ShoppingListItems.Where(i => i.FridgeId == fridgeId)).ToListAsync(ct);
        return
        [
            .. items
                .OrderBy(m => m.Item.Checked)
                .ThenBy(m => m.Product is null ? 1 : 0)
                .ThenBy(m => m.Product?.Category ?? ProductCategory.Other)
                .ThenBy(m => m.Product?.NormalizedName ?? NameNormalizer.Normalize(m.Item.Text ?? ""), StringComparer.Ordinal)
                .ThenBy(m => m.Item.CreatedAt)
                .ThenBy(m => m.Item.Id),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShoppingListItemModel>> AddAsync(
        Guid fridgeId, IReadOnlyList<ShoppingListItemInput> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        await access.RequireMemberAsync(fridgeId, ct);

        var errors = new ValidationErrors();
        if (items.Count is 0 or > MaxBatch)
        {
            errors.Add("items", "out_of_range");
            errors.ThrowIfAny();
        }

        var productIds = items.Where(i => i.ProductId is not null).Select(i => i.ProductId!.Value).Distinct().ToList();
        var known = (await db.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        for (var i = 0; i < items.Count; i++)
        {
            ValidateWrite(items[i], $"items[{i}]", known, errors);
        }

        ThrowIfAny(errors);

        var open = await db.ShoppingListItems
            .Where(i => i.FridgeId == fridgeId && !i.Checked && i.ProductId != null && productIds.Contains(i.ProductId.Value))
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);
        var byProduct = new Dictionary<Guid, ShoppingListItem>();
        foreach (var item in open)
        {
            byProduct.TryAdd(item.ProductId!.Value, item);
        }

        var now = time.GetUtcNow();
        var touched = new List<ShoppingListItem>();
        foreach (var input in items)
        {
            ShoppingListItem target;
            if (input.ProductId is { } productId)
            {
                if (byProduct.TryGetValue(productId, out var existing))
                {
                    existing.Amount = Sum(existing.Amount, input.Amount);
                    target = existing;
                }
                else
                {
                    target = NewItem(fridgeId, productId, null, input.Amount, now);
                    byProduct[productId] = target;
                }
            }
            else
            {
                target = NewItem(fridgeId, null, input.Text!.Trim(), null, now);
            }

            if (!touched.Contains(target))
            {
                touched.Add(target);
            }
        }

        await db.SaveChangesAsync(ct);
        return await LoadInOrderAsync([.. touched.Select(t => t.Id)], ct);
    }

    /// <inheritdoc />
    public async Task<ShoppingListItemModel> PatchAsync(Guid fridgeId, Guid itemId, ShoppingListItemPatchInput patch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        await access.RequireMemberAsync(fridgeId, ct);
        var item = await db.ShoppingListItems.SingleOrDefaultAsync(i => i.Id == itemId && i.FridgeId == fridgeId, ct)
            ?? throw new NotFoundException(ErrorCodes.ShoppingItemNotFound);

        var errors = new ValidationErrors();
        if (patch.Amount is { } amount)
        {
            if (item.ProductId is null)
            {
                errors.Add("amount", ErrorCodes.ShoppingItemInvalid);
            }
            else if (amount <= 0)
            {
                errors.Add("amount", "out_of_range");
            }
        }

        var text = patch.Text?.Trim();
        if (patch.Text is not null)
        {
            if (item.ProductId is not null)
            {
                errors.Add("text", ErrorCodes.ShoppingItemInvalid);
            }
            else if (text!.Length == 0)
            {
                errors.Add("text", "required");
            }
            else if (text.Length > MaxTextLength)
            {
                errors.Add("text", "too_long");
            }
        }

        ThrowIfAny(errors);

        if (item.Version != patch.Version)
        {
            throw new ConflictException(ErrorCodes.ConcurrencyConflict, "The entry was changed by someone else.");
        }

        if (patch.Checked is { } isChecked)
        {
            item.Checked = isChecked;
        }

        if (patch.Amount is not null)
        {
            item.Amount = patch.Amount;
        }

        if (text is not null)
        {
            item.Text = text;
        }

        await db.SaveChangesAsync(ct);
        return (await LoadInOrderAsync([item.Id], ct))[0];
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid fridgeId, Guid itemId, CancellationToken ct)
    {
        await access.RequireMemberAsync(fridgeId, ct);
        var item = await db.ShoppingListItems.SingleOrDefaultAsync(i => i.Id == itemId && i.FridgeId == fridgeId, ct)
            ?? throw new NotFoundException(ErrorCodes.ShoppingItemNotFound);
        db.ShoppingListItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<CompleteShoppingListModel> CompleteAsync(Guid fridgeId, bool moveToFridge, CancellationToken ct)
    {
        await access.RequireMemberAsync(fridgeId, ct);
        var me = currentUser.Id;
        var loaded = await db.ShoppingListItems
            .Include(i => i.Product)
            .Where(i => i.FridgeId == fridgeId && i.Checked)
            .ToListAsync(ct);
        // List order (as in ListAsync), so createdFridgeItems is deterministic: a batch shares one CreatedAt.
        var checkedItems = loaded
            .OrderBy(i => i.Product is null ? 1 : 0)
            .ThenBy(i => i.Product?.Category ?? ProductCategory.Other)
            .ThenBy(i => i.Product?.NormalizedName ?? NameNormalizer.Normalize(i.Text ?? ""), StringComparer.Ordinal)
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToList();

        var now = time.GetUtcNow();
        var created = new List<FridgeItem>();
        var removed = 0;
        foreach (var item in checkedItems)
        {
            if (moveToFridge && item.Product is { } product)
            {
                var package = item.Amount ?? product.DefaultPackageAmount;
                if (package is not > 0)
                {
                    // Nothing to size the package with: keep the entry so the user can add an amount.
                    continue;
                }

                var fridgeItem = new FridgeItem
                {
                    FridgeId = fridgeId,
                    ProductId = product.Id,
                    PackageAmount = package.Value,
                    RemainingPercent = 100m,
                    AddedById = me,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.FridgeItems.Add(fridgeItem);
                created.Add(fridgeItem);
            }

            db.ShoppingListItems.Remove(item);
            removed++;
        }

        if (removed == 0)
        {
            return new CompleteShoppingListModel(0, []);
        }

        await db.SaveChangesAsync(ct);
        var ids = created.Select(c => c.Id).ToList();
        var models = created.Count == 0
            ? []
            : await FridgeItemQueries.Project(db, db.FridgeItems.Where(i => ids.Contains(i.Id))).ToListAsync(ct);
        return new CompleteShoppingListModel(removed, [.. models.OrderBy(m => ids.IndexOf(m.Item.Id))]);
    }

    private IQueryable<ShoppingListItemModel> Project(IQueryable<ShoppingListItem> items)
    {
        var users = db.Users;
        return items
            .AsNoTracking()
            .Select(i => new ShoppingListItemModel(
                i,
                i.Product,
                new FridgeUserRef(i.AddedById, users.Where(u => u.Id == i.AddedById).Select(u => u.Nickname).FirstOrDefault() ?? "")));
    }

    private async Task<IReadOnlyList<ShoppingListItemModel>> LoadInOrderAsync(List<Guid> ids, CancellationToken ct)
    {
        var models = await Project(db.ShoppingListItems.Where(i => ids.Contains(i.Id))).ToListAsync(ct);
        return [.. models.OrderBy(m => ids.IndexOf(m.Item.Id))];
    }

    private ShoppingListItem NewItem(Guid fridgeId, Guid? productId, string? text, decimal? amount, DateTimeOffset now)
    {
        var item = new ShoppingListItem
        {
            FridgeId = fridgeId,
            ProductId = productId,
            Text = text,
            Amount = amount,
            AddedById = currentUser.Id,
            CreatedAt = now,
        };
        db.ShoppingListItems.Add(item);
        return item;
    }

    private static void ValidateWrite(ShoppingListItemInput input, string path, HashSet<Guid> knownProducts, ValidationErrors errors)
    {
        var hasText = !string.IsNullOrWhiteSpace(input.Text);
        if (input.ProductId is { } productId)
        {
            if (input.Text is not null)
            {
                errors.Add(path, ErrorCodes.ShoppingItemInvalid);
            }
            else if (!knownProducts.Contains(productId))
            {
                errors.Add($"{path}.productId", ErrorCodes.ProductNotFound);
            }

            if (input.Amount is <= 0)
            {
                errors.Add($"{path}.amount", "out_of_range");
            }

            return;
        }

        if (!hasText || input.Amount is not null)
        {
            errors.Add(path, ErrorCodes.ShoppingItemInvalid);
        }
        else if (input.Text!.Trim().Length > MaxTextLength)
        {
            errors.Add($"{path}.text", "too_long");
        }
    }

    /// <summary>
    /// Like <see cref="ValidationErrors.ThrowIfAny"/>, but the top-level code is <c>shopping_item.invalid</c> when that
    /// is the only problem (neither or both of product and text; an amount on free text).
    /// </summary>
    private static void ThrowIfAny(ValidationErrors errors)
    {
        try
        {
            errors.ThrowIfAny();
        }
        catch (DomainValidationException e) when (e.Errors.Values.SelectMany(v => v).All(c => c == ErrorCodes.ShoppingItemInvalid))
        {
            throw new DomainValidationException(ErrorCodes.ShoppingItemInvalid, e.Errors);
        }
    }

    private static decimal? Sum(decimal? a, decimal? b) => a is null ? b : b is null ? a : a + b;
}
