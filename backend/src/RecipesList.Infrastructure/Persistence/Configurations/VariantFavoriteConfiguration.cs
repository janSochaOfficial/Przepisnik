using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Favorites;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class VariantFavoriteConfiguration : IEntityTypeConfiguration<VariantFavorite>
{
    public void Configure(EntityTypeBuilder<VariantFavorite> b)
    {
        b.HasKey(x => new { x.UserId, x.VariantId });
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.VariantId);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Variant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
    }
}
