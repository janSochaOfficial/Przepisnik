using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Recipes;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class RecipeStepConfiguration : IEntityTypeConfiguration<RecipeStep>
{
    public void Configure(EntityTypeBuilder<RecipeStep> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.RecipeId, x.Section, x.Position });
        b.Property(x => x.Blocks).HasJsonbConversion();
    }
}
