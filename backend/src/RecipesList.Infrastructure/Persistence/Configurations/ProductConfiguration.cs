using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Catalog;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(80);
        b.Property(x => x.NormalizedName).HasMaxLength(80);
        b.HasIndex(x => x.NormalizedName).IsUnique();
        b.HasIndex(x => x.NormalizedName, "ix_products_normalized_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        b.HasIndex(x => x.Category);
        b.Property(x => x.PieceForms).HasJsonbConversion();

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
