using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Favorites;
using RecipesList.Core.Recipes;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class RecipeFavoriteConfiguration : IEntityTypeConfiguration<RecipeFavorite>
{
    public void Configure(EntityTypeBuilder<RecipeFavorite> b)
    {
        b.HasKey(x => new { x.UserId, x.RecipeId });
        // "Ulubione" lists the newest favourites first.
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.RecipeId);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}
