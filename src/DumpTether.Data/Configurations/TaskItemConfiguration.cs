using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("task_items", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_task_items_archive_requires_resolution",
                "(archived_at IS NULL AND archive_resolution_id IS NULL) OR (archived_at IS NOT NULL AND archive_resolution_id IS NOT NULL)");
        });

        builder.HasKey(taskItem => taskItem.Id);

        builder.Property(taskItem => taskItem.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(taskItem => taskItem.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(taskItem => taskItem.ProjectId)
            .HasColumnName("project_id");

        builder.Property(taskItem => taskItem.TaskTemplateId)
            .HasColumnName("task_template_id");

        builder.Property(taskItem => taskItem.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(taskItem => taskItem.Status)
            .HasColumnName("status")
            .HasMaxLength(120);

        builder.Property(taskItem => taskItem.Category)
            .HasColumnName("category")
            .HasMaxLength(120);

        builder.Property(taskItem => taskItem.Color)
            .HasColumnName("color")
            .HasMaxLength(7);

        builder.Property(taskItem => taskItem.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(taskItem => taskItem.LastViewedAt)
            .HasColumnName("last_viewed_at");

        builder.Property(taskItem => taskItem.LastTouchedAt)
            .HasColumnName("last_touched_at")
            .IsRequired();

        builder.Property(taskItem => taskItem.FollowUpAt)
            .HasColumnName("follow_up_at");

        builder.Property(taskItem => taskItem.ArchivedAt)
            .HasColumnName("archived_at");

        builder.Property(taskItem => taskItem.ArchiveResolutionId)
            .HasColumnName("archive_resolution_id");

        builder.Ignore(taskItem => taskItem.FieldValues);
        builder.Ignore(taskItem => taskItem.TimelineEntries);

        builder.HasIndex(taskItem => new
        {
            taskItem.WorkspaceId,
            taskItem.ArchivedAt,
            taskItem.LastTouchedAt
        });

        builder.HasIndex(taskItem => new
        {
            taskItem.WorkspaceId,
            taskItem.ProjectId,
            taskItem.ArchivedAt
        });

        builder.HasIndex(taskItem => taskItem.FollowUpAt);

        builder.HasIndex(taskItem => new
        {
            taskItem.WorkspaceId,
            taskItem.Category
        });

        builder.HasIndex(taskItem => new
        {
            taskItem.WorkspaceId,
            taskItem.Color
        });

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(taskItem => taskItem.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany("_taskItems")
            .HasForeignKey(taskItem => taskItem.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TaskTemplate>()
            .WithMany()
            .HasForeignKey(taskItem => taskItem.TaskTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ArchiveResolution>()
            .WithMany()
            .HasForeignKey(taskItem => taskItem.ArchiveResolutionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
