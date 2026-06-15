using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class TaskTimelineEntryFieldValueConfiguration : IEntityTypeConfiguration<TaskTimelineEntryFieldValue>
{
    public void Configure(EntityTypeBuilder<TaskTimelineEntryFieldValue> builder)
    {
        builder.ToTable("task_timeline_entry_field_values");

        builder.HasKey(fieldValue => fieldValue.Id);

        builder.Property(fieldValue => fieldValue.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(fieldValue => fieldValue.TaskTimelineEntryId)
            .HasColumnName("task_timeline_entry_id")
            .IsRequired();

        builder.Property(fieldValue => fieldValue.FieldDefinitionId)
            .HasColumnName("field_definition_id")
            .IsRequired();

        builder.Property(fieldValue => fieldValue.ValueJson)
            .HasColumnName("value")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(fieldValue => fieldValue.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(fieldValue => new
            {
                fieldValue.TaskTimelineEntryId,
                fieldValue.FieldDefinitionId
            })
            .IsUnique();

        builder.HasOne<FieldDefinition>()
            .WithMany()
            .HasForeignKey(fieldValue => fieldValue.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
