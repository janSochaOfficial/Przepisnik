using RecipesList.Core.Common;
using RecipesList.Core.Fridges;
using RecipesList.Core.Recipes;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.IntegrationTests.Infrastructure;

/// <summary>Builders for arranging data directly in the database (TECHNICAL §6: no magic GUIDs inline).</summary>
public static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public static User User(string nickname) => new()
    {
        GoogleSubject = $"test|{nickname}",
        Email = $"{nickname}@test.local",
        Nickname = nickname,
        NormalizedNickname = NameNormalizer.Normalize(nickname),
        CreatedAt = Now,
    };

    /// <summary>A fridge owned by <paramref name="owner"/> with the given extra members.</summary>
    public static Fridge Fridge(string name, User owner, params User[] members)
    {
        var fridge = new Fridge { Name = name, OwnerId = owner.Id, CreatedAt = Now };
        fridge.Members.Add(new FridgeMember { FridgeId = fridge.Id, UserId = owner.Id, Role = FridgeRole.Owner, JoinedAt = Now });
        foreach (var member in members)
        {
            fridge.Members.Add(new FridgeMember { FridgeId = fridge.Id, UserId = member.Id, Role = FridgeRole.Member, JoinedAt = Now });
        }

        return fridge;
    }

    public static Recipe Recipe(User author, VisibilityLevel visibility, string title = "Test recipe") => new()
    {
        AuthorId = author.Id,
        Title = title,
        NormalizedTitle = NameNormalizer.Normalize(title),
        Visibility = visibility,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    public static Variant Variant(Recipe recipe, User author, VisibilityLevel visibility, string name = "Test variant") => new()
    {
        RecipeId = recipe.Id,
        AuthorId = author.Id,
        Name = name,
        Visibility = visibility,
        CreatedAt = Now,
        UpdatedAt = Now,
    };
}
