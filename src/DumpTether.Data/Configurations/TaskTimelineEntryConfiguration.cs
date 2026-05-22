using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class TaskTimelineEntryConfiguration : IEntityTypeConfiguration<TaskTimelineEntry>
{
    public void Configure(EntityTypeBuilder<TaskTimelineEntry> builder)
    {
        builder.ToTable("task_timeline_entries");

        builder.HasKey(taskTimelineEntry => taskTimelineEntry.Id);

        builder.Property(taskTimelineEntry => taskTimelineEntry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(taskTimelineEntry => taskTimelineEntry.TaskItemId)
            .HasColumnName("task_item_id")
            .IsRequired();

        builder.Property(taskTimelineEntry => taskTimelineEntry.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(taskTimelineEntry => taskTimelineEntry.Summary)
            .HasColumnName("summary")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(taskTimelineEntry => taskTimelineEntry.Details)
            .HasColumnName("details")
            .HasMaxLength(4000);

        builder.Property(taskTimelineEntry => taskTimelineEntry.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(taskTimelineEntry => taskTimelineEntry.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(taskTimelineEntry => taskTimelineEntry.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(taskTimelineEntry => new
        {
            taskTimelineEntry.TaskItemId,
            taskTimelineEntry.OccurredAt
        });

        builder.HasOne<TaskItem>()
            .WithMany("_timelineEntries")
            .HasForeignKey(taskTimelineEntry => taskTimelineEntry.TaskItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
