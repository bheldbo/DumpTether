using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class SyncRootConfiguration : IEntityTypeConfiguration<SyncRoot>
{
    public void Configure(EntityTypeBuilder<SyncRoot> builder)
    {
        builder.ToTable("sync_roots");

        builder.HasKey(syncRoot => syncRoot.Id);

        builder.Property(syncRoot => syncRoot.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(syncRoot => syncRoot.LocalWorkspaceId)
            .HasColumnName("local_workspace_id")
            .IsRequired();

        builder.Property(syncRoot => syncRoot.RemoteWorkspaceId)
            .HasColumnName("remote_workspace_id");

        builder.Property(syncRoot => syncRoot.CloudUserId)
            .HasColumnName("cloud_user_id");

        builder.Property(syncRoot => syncRoot.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(syncRoot => syncRoot.Origin)
            .HasColumnName("origin")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(syncRoot => syncRoot.RemoteAccessKind)
            .HasColumnName("remote_access_kind")
            .HasMaxLength(40);

        builder.Property(syncRoot => syncRoot.RemoteRole)
            .HasColumnName("remote_role")
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(syncRoot => syncRoot.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(syncRoot => syncRoot.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(syncRoot => syncRoot.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(syncRoot => syncRoot.LastSyncedAt)
            .HasColumnName("last_synced_at");

        builder.Ignore(syncRoot => syncRoot.Mappings);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(syncRoot => syncRoot.LocalWorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(syncRoot => syncRoot.LocalWorkspaceId)
            .IsUnique();

        builder.HasIndex(syncRoot => new
            {
                syncRoot.CloudUserId,
                syncRoot.RemoteWorkspaceId
            })
            .IsUnique()
            .HasFilter("cloud_user_id IS NOT NULL AND remote_workspace_id IS NOT NULL");
    }
}
