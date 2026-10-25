using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Recipes;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class UserRecipeCookConfiguration : IEntityTypeConfiguration<UserRecipeCook>
{
    public void Configure(EntityTypeBuilder<UserRecipeCook> b)
    {
        b.HasKey(x => new { x.UserId, x.RecipeId });
        b.HasIndex(x => x.RecipeId);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}
