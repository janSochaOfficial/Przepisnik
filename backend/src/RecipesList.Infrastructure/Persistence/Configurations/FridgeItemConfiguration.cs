using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Fridges;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class FridgeItemConfiguration : IEntityTypeConfiguration<FridgeItem>
{
    public void Configure(EntityTypeBuilder<FridgeItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PackageAmount).HasColumnType("numeric");
        b.Property(x => x.RemainingPercent).HasPrecision(5, 2);
        b.Property(x => x.Note).HasMaxLength(300);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.FridgeId, x.ProductId });
        b.HasIndex(x => x.ProductId);

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.AddedById).OnDelete(DeleteBehavior.Restrict);
    }
}
