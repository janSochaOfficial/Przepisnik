using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Fridges;
using RecipesList.Core.Recipes;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class DeductionConfiguration : IEntityTypeConfiguration<Deduction>
{
    public void Configure(EntityTypeBuilder<Deduction> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Lines).HasJsonbConversion();
        b.HasIndex(x => new { x.FridgeId, x.CreatedAt });

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Variant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.SetNull);
    }
}
