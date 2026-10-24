using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Recipes;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class RecipeCookCountConfiguration : IEntityTypeConfiguration<RecipeCookCount>
{
    public void Configure(EntityTypeBuilder<RecipeCookCount> b)
    {
        b.HasKey(x => x.RecipeId);
        b.HasOne<Recipe>().WithOne().HasForeignKey<RecipeCookCount>(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}
