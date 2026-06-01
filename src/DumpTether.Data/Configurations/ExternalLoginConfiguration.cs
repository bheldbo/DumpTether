using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");

        builder.HasKey(login => login.Id);

        builder.Property(login => login.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(login => login.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(login => login.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(login => login.ProviderUserId)
            .HasColumnName("provider_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(login => login.EmailAtLogin)
            .HasColumnName("email_at_login")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(login => login.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(login => login.LastUsedAt)
            .HasColumnName("last_used_at")
            .IsRequired();

        builder.HasIndex(login => new
        {
            login.Provider,
            login.ProviderUserId
        }).IsUnique();

        builder.HasIndex(login => login.UserId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
