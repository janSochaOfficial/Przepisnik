using Microsoft.EntityFrameworkCore;
using RecipesList.Api.Features.Units;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Infrastructure.Persistence;

namespace RecipesList.Api.Features.Catalog;

/// <summary>Admin edit and merge of products (PROJECT §7.3, API.md §2).</summary>
internal sealed class ProductAdmin(AppDbContext db, ICurrentUser currentUser, ProductCatalog catalog)
{
    public async Task<Product> UpdateAsync(Guid id, ProductInput input, CancellationToken ct)
    {
        RequireAdmin();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.ProductNotFound);

        ProductRules.Validate(input);
        var name = ProductRules.CleanName(input.Name);
        var normalized = NameNormalizer.Normalize(name);
        await catalog.ThrowIfDuplicateAsync(normalized, id, ct);

        // Changing what a product is measured in would silently corrupt the amounts stored against it.
        if (product.MeasureKind != input.MeasureKind && await IsReferencedAsync(id, ct))
        {
            throw new DomainValidationException(
                ErrorCodes.ProductInvalidCombination,
                new Dictionary<string, string[]> { ["measureKind"] = ["in_use"] });
        }

        product.Name = name;
        product.NormalizedName = normalized;
        ProductMapping.Apply(product, input);
        product.IsVerified = true;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            await catalog.ThrowIfDuplicateAsync(normalized, id, ct);
            throw;
        }

        return product;
    }

    /// <summary>
    /// Re-points every reference to <paramref name="sourceId"/> at <paramref name="targetId"/> and deletes the source, in
    /// one transaction: recipe ingredients, fridge items, shopping-list items, product preferences (duplicates are
    /// merged by summing weights), and the jsonb documents that mention the product (variant ops, selection memory,
    /// deduction lines).
    /// </summary>
    public async Task MergeAsync(Guid sourceId, Guid targetId, CancellationToken ct)
    {
        RequireAdmin();
        var source = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sourceId, ct)
            ?? throw new NotFoundException(ErrorCodes.ProductNotFound);
        var target = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == targetId, ct)
            ?? throw new NotFoundException(ErrorCodes.ProductNotFound);

        if (sourceId == targetId)
        {
            throw new DomainValidationException(
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { ["targetId"] = ["same_as_source"] });
        }

        if (source.MeasureKind != target.MeasureKind)
        {
            throw new DomainValidationException(ErrorCodes.ProductMergeIncompatible);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.RecipeIngredients.Where(i => i.ProductId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ProductId, targetId), ct);
        await db.FridgeItems.Where(i => i.ProductId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ProductId, targetId), ct);
        await db.ShoppingListItems.Where(i => i.ProductId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ProductId, targetId), ct);

        await MergePreferencesAsync(sourceId, targetId, ct);
        await RepointJsonbAsync(sourceId, targetId, ct);

        // Units bound to the source move with it (PROJECT §5.6); clashing names get " (2)".
        await UnitAdmin.MoveProductUnitsAsync(db, sourceId, targetId, ct);

        await db.Products.Where(p => p.Id == sourceId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void RequireAdmin()
    {
        if (!currentUser.IsAdmin)
        {
            throw new ForbiddenException(ErrorCodes.AdminRequired);
        }
    }

    private async Task<bool> IsReferencedAsync(Guid productId, CancellationToken ct) =>
        await db.RecipeIngredients.AnyAsync(i => i.ProductId == productId, ct)
        || await db.FridgeItems.AnyAsync(i => i.ProductId == productId, ct)
        || await db.ShoppingListItems.AnyAsync(i => i.ProductId == productId, ct);

    private async Task MergePreferencesAsync(Guid source, Guid target, CancellationToken ct)
    {
        // A swap between the two products becomes meaningless.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DELETE FROM product_preferences
            WHERE (from_product_id = {source} AND to_product_id = {target})
               OR (from_product_id = {target} AND to_product_id = {source})
            """,
            ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO product_preferences (user_id, from_product_id, to_product_id, weight)
            SELECT user_id, {target}, to_product_id, weight FROM product_preferences WHERE from_product_id = {source}
            ON CONFLICT (user_id, from_product_id, to_product_id)
            DO UPDATE SET weight = product_preferences.weight + EXCLUDED.weight
            """,
            ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM product_preferences WHERE from_product_id = {source}", ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO product_preferences (user_id, from_product_id, to_product_id, weight)
            SELECT user_id, from_product_id, {target}, weight FROM product_preferences WHERE to_product_id = {source}
            ON CONFLICT (user_id, from_product_id, to_product_id)
            DO UPDATE SET weight = product_preferences.weight + EXCLUDED.weight
            """,
            ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM product_preferences WHERE to_product_id = {source}", ct);
    }

    /// <summary>
    /// Replaces the product's id in every jsonb document that can hold it. Ids are v7 GUIDs, unique across all kinds of
    /// entities, so a text replacement of the quoted id changes <c>productId</c> fields (and the <c>&lt;productId&gt;:&lt;amount&gt;</c>
    /// option keys of selection memory) and nothing else.
    /// </summary>
    private async Task RepointJsonbAsync(Guid source, Guid target, CancellationToken ct)
    {
        var from = source.ToString("D");
        var to = target.ToString("D");

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE variants SET ingredient_ops = replace(ingredient_ops::text, {from}, {to})::jsonb
            WHERE strpos(ingredient_ops::text, {from}) > 0
            """,
            ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE recipe_selections
            SET picks = replace(picks::text, {from}, {to})::jsonb,
                scaling = replace(scaling::text, {from}, {to})::jsonb
            WHERE strpos(picks::text, {from}) > 0 OR strpos(coalesce(scaling::text, ''), {from}) > 0
            """,
            ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE deductions SET lines = replace(lines::text, {from}, {to})::jsonb
            WHERE strpos(lines::text, {from}) > 0
            """,
            ct);
    }
}
