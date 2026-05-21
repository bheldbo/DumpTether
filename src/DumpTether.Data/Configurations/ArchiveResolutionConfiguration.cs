using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class ArchiveResolutionConfiguration : IEntityTypeConfiguration<ArchiveResolution>
{
    public void Configure(EntityTypeBuilder<ArchiveResolution> builder)
    {
        builder.ToTable("archive_resolutions");

        builder.HasKey(archiveResolution => archiveResolution.Id);

        builder.Property(archiveResolution => archiveResolution.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(archiveResolution => archiveResolution.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(archiveResolution => archiveResolution.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(archiveResolution => archiveResolution.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(archiveResolution => archiveResolution.RequiresExplanation)
            .HasColumnName("requires_explanation")
            .IsRequired();

        builder.Property(archiveResolution => archiveResolution.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(archiveResolution => archiveResolution.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(archiveResolution => new
            {
                archiveResolution.WorkspaceId,
                archiveResolution.Name
            })
            .IsUnique();

        builder.HasOne<Workspace>()
            .WithMany("_archiveResolutions")
            .HasForeignKey(archiveResolution => archiveResolution.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
