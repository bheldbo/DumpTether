using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class FieldValueConfiguration : IEntityTypeConfiguration<FieldValue>
{
    public void Configure(EntityTypeBuilder<FieldValue> builder)
    {
        builder.ToTable("field_values");

        builder.HasKey(fieldValue => fieldValue.Id);

        builder.Property(fieldValue => fieldValue.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(fieldValue => fieldValue.TaskItemId)
            .HasColumnName("task_item_id")
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
                fieldValue.TaskItemId,
                fieldValue.FieldDefinitionId
            })
            .IsUnique();

        builder.HasOne<TaskItem>()
            .WithMany("_fieldValues")
            .HasForeignKey(fieldValue => fieldValue.TaskItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FieldDefinition>()
            .WithMany()
            .HasForeignKey(fieldValue => fieldValue.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
