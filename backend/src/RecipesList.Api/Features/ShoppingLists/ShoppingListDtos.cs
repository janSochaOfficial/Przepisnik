using System.ComponentModel.DataAnnotations;
using RecipesList.Api.Features.Fridges;
using RecipesList.Api.Features.Units;
using RecipesList.Core.Quantities;
using RecipesList.Core.ShoppingLists;

namespace RecipesList.Api.Features.ShoppingLists;

/// <summary>A shopping-list entry (API.md §7 <c>ShoppingListItem</c>): exactly one of <c>Product</c> and <c>Text</c>.</summary>
internal sealed record ShoppingListItemDto(
    Guid Id,
    uint Version,
    FridgeProductRefDto? Product,
    string? Text,
    QuantityDto? Quantity,
    decimal? Amount,
    bool Checked,
    FridgeUserRefDto AddedBy,
    DateTimeOffset CreatedAt)
{
    public static ShoppingListItemDto From(ShoppingListItemModel m) => new(
        m.Item.Id,
        m.Item.Version,
        m.Product is null ? null : FridgeProductRefDto.From(m.Product),
        m.Item.Text,
        QuantityDto.From(m.Item.Quantity, m.CustomUnit),
        m.Item.Amount,
        m.Item.Checked,
        FridgeUserRefDto.From(m.AddedBy),
        m.Item.CreatedAt);
}

/// <summary>One entry to add: a product (optional amount in base units or quantity, exclusive) or free text.</summary>
internal sealed record ShoppingListItemWriteRequest(Guid? ProductId, string? Text, decimal? Amount, Quantity? Quantity = null);

/// <summary>Body of <c>POST /fridges/{id}/shopping-list/items</c> (1–100 entries).</summary>
internal sealed record ShoppingListItemsWriteRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public required IReadOnlyList<ShoppingListItemWriteRequest> Items { get; init; }
}

/// <summary>Body of <c>PATCH /fridges/{id}/shopping-list/items/{itemId}</c>; null fields are left unchanged.</summary>
internal sealed record ShoppingListItemPatchRequest
{
    public required uint Version { get; init; }

    public bool? Checked { get; init; }

    /// <summary>Sets the unit and the amount.</summary>
    public Quantity? Quantity { get; init; }

    /// <summary>Sets a base amount and drops the chosen unit.</summary>
    public decimal? Amount { get; init; }

    /// <summary>True removes the amount and the unit.</summary>
    public bool? ClearAmount { get; init; }

    [MaxLength(200)]
    public string? Text { get; init; }
}

/// <summary>Body of <c>POST /fridges/{id}/shopping-list/complete</c>.</summary>
internal sealed record CompleteShoppingListRequest
{
    public required bool MoveToFridge { get; init; }
}

/// <summary>What <c>complete</c> removed and created.</summary>
internal sealed record CompleteShoppingListResultDto(int Removed, IReadOnlyList<FridgeItemDto> CreatedFridgeItems);
