using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class SyncMappingConfiguration : IEntityTypeConfiguration<SyncMapping>
{
    public void Configure(EntityTypeBuilder<SyncMapping> builder)
    {
        builder.ToTable("sync_mappings");

        builder.HasKey(mapping => mapping.Id);

        builder.Property(mapping => mapping.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(mapping => mapping.SyncRootId)
            .HasColumnName("sync_root_id")
            .IsRequired();

        builder.Property(mapping => mapping.EntityType)
            .HasColumnName("entity_type")
            .HasConversion<string>()
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(mapping => mapping.LocalId)
            .HasColumnName("local_id")
            .IsRequired();

        builder.Property(mapping => mapping.RemoteId)
            .HasColumnName("remote_id");

        builder.Property(mapping => mapping.LastRemoteVersion)
            .HasColumnName("last_remote_version")
            .HasMaxLength(200);

        builder.Property(mapping => mapping.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(mapping => mapping.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(1000);

        builder.Property(mapping => mapping.LastAttemptedAt)
            .HasColumnName("last_attempted_at");

        builder.Property(mapping => mapping.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(mapping => mapping.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(mapping => mapping.LastSyncedAt)
            .HasColumnName("last_synced_at");

        builder.HasOne<SyncRoot>()
            .WithMany()
            .HasForeignKey(mapping => mapping.SyncRootId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(mapping => new
            {
                mapping.SyncRootId,
                mapping.EntityType,
                mapping.LocalId
            })
            .IsUnique();

        builder.HasIndex(mapping => new
            {
                mapping.SyncRootId,
                mapping.EntityType,
                mapping.RemoteId
            })
            .IsUnique()
            .HasFilter("remote_id IS NOT NULL");
    }
}
