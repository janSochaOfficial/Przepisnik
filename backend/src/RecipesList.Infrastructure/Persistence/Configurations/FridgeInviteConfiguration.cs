using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Fridges;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class FridgeInviteConfiguration : IEntityTypeConfiguration<FridgeInvite>
{
    public void Configure(EntityTypeBuilder<FridgeInvite> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(64);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.FridgeId);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
