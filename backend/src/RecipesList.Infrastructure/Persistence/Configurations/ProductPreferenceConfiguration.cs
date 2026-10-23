using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Catalog;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class ProductPreferenceConfiguration : IEntityTypeConfiguration<ProductPreference>
{
    public void Configure(EntityTypeBuilder<ProductPreference> b)
    {
        b.HasKey(x => new { x.UserId, x.FromProductId, x.ToProductId });
        b.HasIndex(x => x.FromProductId);
        b.HasIndex(x => x.ToProductId);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.FromProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ToProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
