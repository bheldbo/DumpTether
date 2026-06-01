using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(session => session.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(session => session.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(session => session.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(session => session.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(session => session.LastSeenAt)
            .HasColumnName("last_seen_at")
            .IsRequired();

        builder.Property(session => session.SessionTokenHash)
            .HasColumnName("session_token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(session => session.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(512);

        builder.Property(session => session.IpAddressHash)
            .HasColumnName("ip_address_hash")
            .HasMaxLength(128);

        builder.Property(session => session.DeviceName)
            .HasColumnName("device_name")
            .HasMaxLength(120);

        builder.HasIndex(session => session.SessionTokenHash)
            .IsUnique();

        builder.HasIndex(session => new
        {
            session.UserId,
            session.ExpiresAt,
            session.RevokedAt
        });

        builder.HasOne<AppUser>()
            .WithMany("_sessions")
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
