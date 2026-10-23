using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipesList.Core.Users;

namespace RecipesList.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.GoogleSubject).HasMaxLength(255);
        b.HasIndex(x => x.GoogleSubject).IsUnique();
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Nickname).HasMaxLength(30);
        b.Property(x => x.NormalizedNickname).HasMaxLength(30);
        b.HasIndex(x => x.NormalizedNickname).IsUnique();
        b.Property(x => x.AvatarUrl).HasMaxLength(2048);

        b.HasOne(x => x.DefaultFridge)
            .WithMany()
            .HasForeignKey(x => x.DefaultFridgeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
