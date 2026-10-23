using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Recipes;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.NormalizedTitle).HasMaxLength(150);
        b.HasIndex(x => x.NormalizedTitle, "ix_recipes_normalized_title_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.AuthorId, x.CreatedAt });
        b.HasIndex(x => new { x.Visibility, x.CreatedAt });

        b.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.CoverImage)
            .WithMany()
            .HasForeignKey(x => x.CoverImageId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.Ingredients)
            .WithOne()
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Steps)
            .WithOne()
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Tags)
            .WithOne()
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Utensils)
            .WithOne()
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Variants)
            .WithOne(x => x.Recipe)
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
