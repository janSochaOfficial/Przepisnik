using System.ComponentModel.DataAnnotations;
using RecipesList.Core.Catalog;
using RecipesList.Core.Fridges;

namespace RecipesList.Api.Features.Fridges;

// Request and response shapes of API.md §6 and §7. Shared shapes (ProductRef, UserRef) are declared here under
// fridge-specific names until a common DTO module exists, so they cannot clash with other features' types.

/// <summary>A user as shown to others (API.md §0 <c>UserRef</c>); never carries the email.</summary>
internal sealed record FridgeUserRefDto(Guid Id, string Nickname)
{
    public static FridgeUserRefDto From(FridgeUserRef u) => new(u.Id, u.Nickname);
}

/// <summary>A catalog product (API.md §0 <c>ProductRef</c>).</summary>
internal sealed record FridgeProductRefDto(
    Guid Id,
    string Name,
    ProductCategory Category,
    MeasureKind MeasureKind,
    DisplayClass DisplayClass,
    bool HasSizes,
    bool AllowHalf,
    decimal? GramsPerMl,
    decimal? GramsPerPiece,
    decimal? DefaultPackageAmount,
    PluralForms? PieceForms,
    bool IsVerified)
{
    public static FridgeProductRefDto From(Product p) => new(
        p.Id, p.Name, p.Category, p.MeasureKind, p.DisplayClass, p.HasSizes, p.AllowHalf,
        p.GramsPerMl, p.GramsPerPiece, p.DefaultPackageAmount, p.PieceForms, p.IsVerified);
}

/// <summary>Body of <c>POST /fridges</c> and <c>PUT /fridges/{id}</c> (1–80 characters after trimming).</summary>
internal sealed record FridgeNameRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public required string Name { get; init; }
}

/// <summary>A fridge from the caller's point of view (API.md §6 <c>FridgeSummary</c>).</summary>
internal sealed record FridgeSummaryDto(Guid Id, string Name, FridgeRole Role, FridgeUserRefDto Owner, int MemberCount, int ItemCount)
{
    public static FridgeSummaryDto From(FridgeSummaryModel m) =>
        new(m.Id, m.Name, m.Role, FridgeUserRefDto.From(m.Owner), m.MemberCount, m.ItemCount);
}

/// <summary>A member of a fridge (UserRef plus role).</summary>
internal sealed record FridgeMemberDto(Guid Id, string Nickname, FridgeRole Role);

/// <summary>A fridge with its members, owner first (API.md §6 <c>FridgeDetail</c>).</summary>
internal sealed record FridgeDetailDto(
    Guid Id,
    string Name,
    FridgeRole Role,
    FridgeUserRefDto Owner,
    int MemberCount,
    int ItemCount,
    IReadOnlyList<FridgeMemberDto> Members)
{
    public static FridgeDetailDto From(FridgeDetailModel m) => new(
        m.Summary.Id, m.Summary.Name, m.Summary.Role, FridgeUserRefDto.From(m.Summary.Owner), m.Summary.MemberCount,
        m.Summary.ItemCount, [.. m.Members.Select(x => new FridgeMemberDto(x.Id, x.Nickname, x.Role))]);
}

/// <summary>An invite link; <c>Url</c> is the frontend route <c>/zaproszenie/&lt;token&gt;</c>.</summary>
internal sealed record InviteDto(Guid Id, string Token, string Url, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt)
{
    public static InviteDto From(InviteModel m) => new(m.Id, m.Token, $"/zaproszenie/{m.Token}", m.ExpiresAt, m.CreatedAt);
}

/// <summary>What an invite is for (API.md §6 <c>InvitePreview</c>).</summary>
internal sealed record InvitePreviewDto(string FridgeName, FridgeUserRefDto Owner, DateTimeOffset ExpiresAt, bool AlreadyMember)
{
    public static InvitePreviewDto From(InvitePreviewModel m) =>
        new(m.FridgeName, FridgeUserRefDto.From(m.Owner), m.ExpiresAt, m.AlreadyMember);
}

/// <summary>One package in a fridge (API.md §6 <c>FridgeItem</c>); <c>RemainingAmount</c> is server-computed.</summary>
internal sealed record FridgeItemDto(
    Guid Id,
    uint Version,
    FridgeProductRefDto Product,
    decimal PackageAmount,
    decimal RemainingPercent,
    decimal RemainingAmount,
    string? Note,
    FridgeUserRefDto AddedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static FridgeItemDto From(FridgeItemModel m) => new(
        m.Item.Id, m.Item.Version, FridgeProductRefDto.From(m.Product), m.Item.PackageAmount, m.Item.RemainingPercent,
        m.RemainingAmount, m.Item.Note, FridgeUserRefDto.From(m.AddedBy), m.Item.CreatedAt, m.Item.UpdatedAt);
}

/// <summary>Body of <c>POST /fridges/{id}/items</c> (API.md §6 <c>FridgeItemWrite</c>).</summary>
internal record FridgeItemWriteRequest
{
    public required Guid ProductId { get; init; }

    /// <summary>Base units, &gt; 0.</summary>
    public required decimal PackageAmount { get; init; }

    /// <summary>0–100; rounded to 2 decimals. 0 is allowed only on <c>PUT</c> (it removes the item).</summary>
    [Range(0, 100)]
    public required decimal RemainingPercent { get; init; }

    [MaxLength(300)]
    public string? Note { get; init; }

    public FridgeItemInput ToInput() => new(ProductId, PackageAmount, RemainingPercent, Note);
}

/// <summary>Body of <c>PUT /fridges/{id}/items/{itemId}</c>: the write shape plus the version last read.</summary>
internal sealed record FridgeItemUpdateRequest : FridgeItemWriteRequest
{
    public required uint Version { get; init; }
}

/// <summary>One line of a deduction (base units, &gt; 0).</summary>
internal sealed record DeductionLineRequest(Guid ProductId, decimal Amount);

/// <summary>Body of <c>POST /fridges/{id}/deductions</c> (API.md §7 <c>DeductionRequest</c>).</summary>
internal sealed record DeductionRequest
{
    /// <summary>For cook counts.</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>Requires <see cref="RecipeId"/>.</summary>
    public Guid? VariantId { get; init; }

    [Required]
    public required IReadOnlyList<DeductionLineRequest> Lines { get; init; }

    public DeductionInput ToInput() => new(RecipeId, VariantId, [.. Lines.Select(l => new DeductionLineInput(l.ProductId, l.Amount))]);
}

/// <summary>What happened to one package.</summary>
internal sealed record DeductionItemResultDto(Guid ItemId, decimal PreviousPercent, decimal NewPercent, bool Removed);

/// <summary>The deduction of one product.</summary>
internal sealed record DeductionResultLineDto(
    Guid ProductId, decimal Requested, decimal Deducted, decimal Shortfall, IReadOnlyList<DeductionItemResultDto> Items);

/// <summary>A recorded deduction (API.md §7 <c>DeductionResult</c>).</summary>
internal sealed record DeductionResultDto(
    Guid Id, bool Undone, DateTimeOffset CreatedAt, bool CookCounted, IReadOnlyList<DeductionResultLineDto> Lines)
{
    /// <summary>An undo never counts a cook, so <paramref name="cookCounted"/> is false there.</summary>
    public static DeductionResultDto From(Deduction d, bool cookCounted = false) => new(
        d.Id,
        d.UndoneAt is not null,
        d.CreatedAt,
        cookCounted,
        [.. d.Lines.Select(l => new DeductionResultLineDto(
            l.ProductId,
            l.Requested,
            l.Deducted,
            l.Shortfall,
            [.. l.Items.Select(i => new DeductionItemResultDto(i.ItemId, i.PreviousPercent, i.NewPercent, i.Removed))]))]);
}
