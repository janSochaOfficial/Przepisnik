namespace RecipesList.Core.Common;

/// <summary>Every stable error code from API.md §9. Features add theirs here (additive only).</summary>
public static class ErrorCodes
{
    public const string AuthInvalidToken = "auth.invalid_token";
    public const string AuthRequired = "auth.required";
    public const string UserOnboardingRequired = "user.onboarding_required";
    public const string NicknameTaken = "nickname.taken";
    public const string NicknameInvalid = "nickname.invalid";
    public const string AdminRequired = "admin.required";

    public const string ProductNotFound = "product.not_found";
    public const string ProductDuplicate = "product.duplicate";
    public const string ProductInvalidCombination = "product.invalid_combination";
    public const string ProductMergeIncompatible = "product.merge_incompatible";
    public const string TagReserved = "tag.reserved";

    public const string ImageInvalidType = "image.invalid_type";
    public const string ImageTooLarge = "image.too_large";
    public const string ImageNotFound = "image.not_found";
    public const string ImageNotOwned = "image.not_owned";

    public const string RecipeNotFound = "recipe.not_found";
    public const string RecipeNotAuthor = "recipe.not_author";
    public const string RecipePublicLocked = "recipe.public_locked";
    public const string RecipeHasVariants = "recipe.has_variants";
    public const string RecipeVisibilityLocked = "recipe.visibility_locked";
    public const string RecipeArchived = "recipe.archived";
    public const string QuantityUnitIncompatible = "quantity.unit_incompatible";
    public const string QuantityInvalid = "quantity.invalid";

    public const string VariantNotFound = "variant.not_found";
    public const string VariantNotAuthor = "variant.not_author";
    public const string VariantNoChanges = "variant.no_changes";
    public const string VariantNameTaken = "variant.name_taken";
    public const string SelectionUnknownSlot = "selection.unknown_slot";

    public const string FridgeNotFound = "fridge.not_found";
    public const string FridgeOwnerRequired = "fridge.owner_required";
    public const string FridgeOwnerCannotLeave = "fridge.owner_cannot_leave";
    public const string InviteNotFound = "invite.not_found";
    public const string FridgeItemNotFound = "fridge_item.not_found";
    public const string DeductionEmpty = "deduction.empty";
    public const string DeductionNotFound = "deduction.not_found";
    public const string DeductionItemsChanged = "deduction.items_changed";
    public const string DeductionAlreadyUndone = "deduction.already_undone";
    public const string ShoppingItemNotFound = "shopping_item.not_found";
    public const string ShoppingItemInvalid = "shopping_item.invalid";

    public const string ImportInvalidFormat = "import.invalid_format";
    public const string ImportUnsupportedVersion = "import.unsupported_version";
    public const string ImportTooLarge = "import.too_large";

    public const string ConcurrencyConflict = "concurrency.conflict";
    public const string ValidationFailed = "validation.failed";
    public const string RequestMissingCsrfHeader = "request.missing_csrf_header";
    public const string RequestNotFound = "request.not_found";
    public const string RequestForbidden = "request.forbidden";
    public const string RequestInvalid = "request.invalid";
    public const string RateLimited = "rate_limited";
    public const string ServerError = "server.error";
}
