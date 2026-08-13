using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(user => user.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(user => user.EmailConfirmedAt)
            .HasColumnName("email_confirmed_at");

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(user => user.HasPasswordCredential)
            .HasColumnName("has_password_credential")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Ignore(user => user.Sessions);
        builder.Ignore(user => user.WorkspaceMemberships);

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique();

    }
}
