using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Recipes;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> b)
    {
        b.HasKey(x => x.Id);
        b.Ignore(x => x.Quantity);
        b.HasIndex(x => new { x.RecipeId, x.Position });
        b.HasIndex(x => x.ProductId);
        b.Property(x => x.Note).HasMaxLength(300);

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // A custom unit in use cannot be deleted (PROJECT §5.6).
        b.HasOne(x => x.CustomUnit)
            .WithMany()
            .HasForeignKey(x => x.QuantityCustomUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
