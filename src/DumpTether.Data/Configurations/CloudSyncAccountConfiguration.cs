using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class CloudSyncAccountConfiguration : IEntityTypeConfiguration<CloudSyncAccount>
{
    public void Configure(EntityTypeBuilder<CloudSyncAccount> builder)
    {
        builder.ToTable("cloud_sync_accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(account => account.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(account => account.CloudApiBaseUrl)
            .HasColumnName("cloud_api_base_url")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(account => account.CloudUserId)
            .HasColumnName("cloud_user_id")
            .IsRequired();

        builder.Property(account => account.CloudEmail)
            .HasColumnName("cloud_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(account => account.CloudDisplayName)
            .HasColumnName("cloud_display_name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(account => account.ProtectedSessionToken)
            .HasColumnName("protected_session_token")
            .HasMaxLength(4096)
            .IsRequired();

        builder.Property(account => account.SessionExpiresAt)
            .HasColumnName("session_expires_at")
            .IsRequired();

        builder.Property(account => account.ConnectedAt)
            .HasColumnName("connected_at")
            .IsRequired();

        builder.Property(account => account.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(account => account.LastVerifiedAt)
            .HasColumnName("last_verified_at");

        builder.Property(account => account.DisconnectedAt)
            .HasColumnName("disconnected_at");

        builder.HasIndex(account => account.UserId)
            .IsUnique();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
