using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class TaskItemShareConfiguration : IEntityTypeConfiguration<TaskItemShare>
{
    public void Configure(EntityTypeBuilder<TaskItemShare> builder)
    {
        builder.ToTable("task_item_shares");

        builder.HasKey(share => share.Id);

        builder.Property(share => share.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(share => share.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(share => share.TaskItemId)
            .HasColumnName("task_item_id")
            .IsRequired();

        builder.Property(share => share.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(share => share.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(share => share.SharedWithUserId)
            .HasColumnName("shared_with_user_id");

        builder.Property(share => share.SharedByUserId)
            .HasColumnName("shared_by_user_id")
            .IsRequired();

        builder.Property(share => share.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(share => share.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(512);

        builder.Property(share => share.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(share => share.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(share => share.AcceptedAt)
            .HasColumnName("accepted_at");

        builder.Property(share => share.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasIndex(share => new
        {
            share.TaskItemId,
            share.NormalizedEmail,
            share.RevokedAt
        });

        builder.HasIndex(share => new
        {
            share.WorkspaceId,
            share.NormalizedEmail,
            share.RevokedAt
        });

        builder.HasIndex(share => share.TokenHash);

        builder.HasOne<TaskItem>()
            .WithMany(taskItem => taskItem.Shares)
            .HasForeignKey(share => share.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(share => share.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(share => share.SharedWithUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(share => share.SharedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
