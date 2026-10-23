using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Fridges;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class FridgeMemberConfiguration : IEntityTypeConfiguration<FridgeMember>
{
    public void Configure(EntityTypeBuilder<FridgeMember> b)
    {
        b.HasKey(x => new { x.FridgeId, x.UserId });
        // Supports VisibilityRules' "shares a fridge" EXISTS and "my fridges" lookups.
        b.HasIndex(x => new { x.UserId, x.FridgeId });

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
