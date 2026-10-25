using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Fridges;
using RecipesList.Core.ShoppingLists;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class ShoppingListItemConfiguration : IEntityTypeConfiguration<ShoppingListItem>
{
    public void Configure(EntityTypeBuilder<ShoppingListItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Text).HasMaxLength(200);
        b.Property(x => x.Amount).HasColumnType("numeric");
        b.Ignore(x => x.Quantity);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.FridgeId, x.Checked });
        b.HasIndex(x => x.ProductId);
        b.ToTable(t => t.HasCheckConstraint(
            "ck_shopping_list_items_product_xor_text",
            "(product_id IS NULL) <> (text IS NULL)"));

        b.HasOne<Fridge>().WithMany().HasForeignKey(x => x.FridgeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // A custom unit in use cannot be deleted (PROJECT §5.6).
        b.HasOne(x => x.CustomUnit)
            .WithMany()
            .HasForeignKey(x => x.QuantityCustomUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AddedById).OnDelete(DeleteBehavior.Restrict);
    }
}
