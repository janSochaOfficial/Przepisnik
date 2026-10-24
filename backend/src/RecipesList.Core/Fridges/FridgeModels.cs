using RecipesList.Core.Catalog;

namespace RecipesList.Core.Fridges;

/// <summary>A user as others see them (never the email).</summary>
public sealed record FridgeUserRef(Guid Id, string Nickname);

/// <summary>A fridge from the caller's point of view (API.md §6 <c>FridgeSummary</c>).</summary>
public sealed record FridgeSummaryModel(Guid Id, string Name, FridgeRole Role, FridgeUserRef Owner, int MemberCount, int ItemCount);

/// <summary>A member of a fridge.</summary>
public sealed record FridgeMemberModel(Guid Id, string Nickname, FridgeRole Role);

/// <summary>A fridge with its members, owner first (API.md §6 <c>FridgeDetail</c>).</summary>
public sealed record FridgeDetailModel(FridgeSummaryModel Summary, IReadOnlyList<FridgeMemberModel> Members);

/// <summary>An active invite link.</summary>
public sealed record InviteModel(Guid Id, string Token, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

/// <summary>What an invite is for (API.md §6 <c>InvitePreview</c>).</summary>
public sealed record InvitePreviewModel(string FridgeName, FridgeUserRef Owner, DateTimeOffset ExpiresAt, bool AlreadyMember);

/// <summary>A fridge item with its product and the user who added it.</summary>
public sealed record FridgeItemModel(FridgeItem Item, Product Product, FridgeUserRef AddedBy)
{
    /// <summary><c>packageAmount * remainingPercent / 100</c> (API.md §6).</summary>
    public decimal RemainingAmount => Item.PackageAmount * Item.RemainingPercent / 100m;
}

/// <summary>The write shape of a fridge item (API.md §6 <c>FridgeItemWrite</c>).</summary>
public sealed record FridgeItemInput(Guid ProductId, decimal PackageAmount, decimal RemainingPercent, string? Note);

/// <summary>One requested line of a deduction (base units).</summary>
public sealed record DeductionLineInput(Guid ProductId, decimal Amount);

/// <summary>A deduction request (API.md §7 <c>DeductionRequest</c>).</summary>
public sealed record DeductionInput(Guid? RecipeId, Guid? VariantId, IReadOnlyList<DeductionLineInput> Lines);
