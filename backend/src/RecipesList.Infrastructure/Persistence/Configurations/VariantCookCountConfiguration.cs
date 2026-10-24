using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Variants;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class VariantCookCountConfiguration : IEntityTypeConfiguration<VariantCookCount>
{
    public void Configure(EntityTypeBuilder<VariantCookCount> b)
    {
        b.HasKey(x => x.VariantId);
        b.HasOne<Variant>().WithOne().HasForeignKey<VariantCookCount>(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
    }
}
