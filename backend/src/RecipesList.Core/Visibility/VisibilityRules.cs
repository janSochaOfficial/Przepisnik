using RecipesList.Core.Common;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;

namespace RecipesList.Core.Visibility;

/// <summary>
/// The single place where recipe and variant visibility is decided (PROJECT §4.4, §6.1; TECHNICAL §3.2).
/// Every recipe or variant read composes with these queries; never re-implement the fridge check inline.
/// Both methods translate to one SQL query; "shares a fridge" is an <c>EXISTS</c> over two <c>fridge_members</c>
/// rows with the same fridge (index <c>ix_fridge_members_user_id_fridge_id</c>).
/// </summary>
public sealed class VisibilityRules(IAppDbContext db)
{
    /// <summary>
    /// Recipes the user may open: their own; public ones; fridge-only ones whose author shares at least one fridge
    /// with the user (computed live, D-22). Archived recipes (D-17) are reachable only by their author and by users
    /// who have a variant of them and could otherwise see them. Callers that list recipes still exclude archived ones.
    /// </summary>
    public IQueryable<Recipe> VisibleRecipes(Guid userId)
    {
        var members = db.FridgeMembers;
        var variants = db.Variants;

        return db.Recipes.Where(r =>
            r.AuthorId == userId
            || ((r.Visibility == VisibilityLevel.Public
                    || (r.Visibility == VisibilityLevel.Fridges
                        && members.Any(mine => mine.UserId == userId
                            && members.Any(theirs => theirs.FridgeId == mine.FridgeId && theirs.UserId == r.AuthorId))))
                && (!r.IsArchived || variants.Any(v => v.RecipeId == r.Id && v.AuthorId == userId))));
    }

    /// <summary>
    /// Variants the user may see: always their own; otherwise the base recipe must be visible to them
    /// (<see cref="VisibleRecipes"/>) and the variant's own visibility, evaluated against the variant's author,
    /// must admit them (PROJECT §6.1). Private variants are visible only to their author.
    /// </summary>
    public IQueryable<Variant> VisibleVariants(Guid userId)
    {
        var members = db.FridgeMembers;
        var recipes = VisibleRecipes(userId);

        return db.Variants.Where(v =>
            v.AuthorId == userId
            || (recipes.Any(r => r.Id == v.RecipeId)
                && (v.Visibility == VisibilityLevel.Public
                    || (v.Visibility == VisibilityLevel.Fridges
                        && members.Any(mine => mine.UserId == userId
                            && members.Any(theirs => theirs.FridgeId == mine.FridgeId && theirs.UserId == v.AuthorId))))));
    }
}
