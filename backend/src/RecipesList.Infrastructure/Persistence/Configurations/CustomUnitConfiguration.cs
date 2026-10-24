using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Catalog;
using RecipesList.Core.Units;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class CustomUnitConfiguration : IEntityTypeConfiguration<CustomUnit>
{
    public void Configure(EntityTypeBuilder<CustomUnit> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(40);
        b.Property(x => x.NormalizedName).HasMaxLength(40);
        b.Property(x => x.Forms).HasJsonbConversion();
        b.Property(x => x.Amount).HasColumnType("numeric");

        // One name per scope; "every product" (null) is a scope of its own (PROJECT §5.6).
        b.HasIndex(x => new { x.NormalizedName, x.ProductId }).IsUnique().AreNullsDistinct(false);
        b.HasIndex(x => x.ProductId);

        b.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
