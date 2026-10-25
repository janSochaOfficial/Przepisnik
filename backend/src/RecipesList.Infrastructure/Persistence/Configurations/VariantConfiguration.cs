using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Variants;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class VariantConfiguration : IEntityTypeConfiguration<Variant>
{
    public void Configure(EntityTypeBuilder<Variant> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(80);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.IngredientOps).HasJsonbConversion();
        b.Property(x => x.StepOps).HasJsonbConversion();
        b.Property(x => x.Version).IsRowVersion();
        b.ToTable(t => t.HasCheckConstraint("ck_variants_servings_override_range", "servings_override BETWEEN 1 AND 100"));
        // "Unique per recipe per author" (API.md §5); also serves the per-recipe lookups.
        b.HasIndex(x => new { x.RecipeId, x.AuthorId, x.Name }).IsUnique();
        b.HasIndex(x => new { x.AuthorId, x.CreatedAt });

        b.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
