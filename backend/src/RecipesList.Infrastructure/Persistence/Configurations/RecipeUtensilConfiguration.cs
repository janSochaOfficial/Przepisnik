using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Recipes;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class RecipeUtensilConfiguration : IEntityTypeConfiguration<RecipeUtensil>
{
    public void Configure(EntityTypeBuilder<RecipeUtensil> b)
    {
        b.HasKey(x => new { x.RecipeId, x.UtensilId });
        b.HasIndex(x => x.UtensilId);

        b.HasOne(x => x.Utensil)
            .WithMany()
            .HasForeignKey(x => x.UtensilId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
