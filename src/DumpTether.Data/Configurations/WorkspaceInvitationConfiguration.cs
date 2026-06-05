using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("workspace_invitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(invitation => invitation.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(invitation => invitation.InvitedByUserId)
            .HasColumnName("invited_by_user_id")
            .IsRequired();

        builder.Property(invitation => invitation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(invitation => invitation.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(invitation => invitation.AcceptedAt)
            .HasColumnName("accepted_at");

        builder.Property(invitation => invitation.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        builder.HasIndex(invitation => new
        {
            invitation.WorkspaceId,
            invitation.NormalizedEmail,
            invitation.AcceptedAt,
            invitation.RevokedAt
        });

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(invitation => invitation.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
