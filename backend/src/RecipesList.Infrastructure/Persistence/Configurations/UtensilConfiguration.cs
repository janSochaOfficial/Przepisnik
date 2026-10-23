using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Catalog;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class UtensilConfiguration : IEntityTypeConfiguration<Utensil>
{
    public void Configure(EntityTypeBuilder<Utensil> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(60);
        b.Property(x => x.NormalizedName).HasMaxLength(60);
        b.HasIndex(x => x.NormalizedName).IsUnique();
        b.HasIndex(x => x.NormalizedName, "ix_utensils_normalized_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
