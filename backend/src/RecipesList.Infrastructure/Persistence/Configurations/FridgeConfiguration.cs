using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Fridges;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class FridgeConfiguration : IEntityTypeConfiguration<Fridge>
{
    public void Configure(EntityTypeBuilder<Fridge> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(80);
        b.HasIndex(x => x.OwnerId);

        b.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.FridgeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany<FridgeInvite>().WithOne().HasForeignKey(x => x.FridgeId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany<FridgeItem>().WithOne().HasForeignKey(x => x.FridgeId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany<Deduction>().WithOne().HasForeignKey(x => x.FridgeId).OnDelete(DeleteBehavior.Cascade);
    }
}
