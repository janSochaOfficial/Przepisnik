using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Recipes;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class RecipeSelectionConfiguration : IEntityTypeConfiguration<RecipeSelection>
{
    public void Configure(EntityTypeBuilder<RecipeSelection> b)
    {
        b.HasKey(x => new { x.UserId, x.RecipeId });
        b.HasIndex(x => x.RecipeId);
        b.Property(x => x.Picks).HasJsonbConversion();
        b.Property(x => x.Scaling).HasJsonbConversion();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}
