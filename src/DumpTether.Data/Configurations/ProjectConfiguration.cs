using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(project => project.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(project => project.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore(project => project.TaskItems);

        builder.HasIndex(project => new
            {
                project.WorkspaceId,
                project.Name
            })
            .IsUnique();

        builder.HasOne<Workspace>()
            .WithMany("_projects")
            .HasForeignKey(project => project.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
