using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class FieldDefinitionConfiguration : IEntityTypeConfiguration<FieldDefinition>
{
    public void Configure(EntityTypeBuilder<FieldDefinition> builder)
    {
        builder.ToTable("field_definitions");

        builder.HasKey(fieldDefinition => fieldDefinition.Id);

        builder.Property(fieldDefinition => fieldDefinition.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(fieldDefinition => fieldDefinition.TaskTemplateId)
            .HasColumnName("task_template_id")
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.Key)
            .HasColumnName("key")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.IsRequired)
            .HasColumnName("is_required")
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(fieldDefinition => new
            {
                fieldDefinition.TaskTemplateId,
                fieldDefinition.Key
            })
            .IsUnique();

        builder.HasIndex(fieldDefinition => new
        {
            fieldDefinition.TaskTemplateId,
            fieldDefinition.SortOrder
        });

        builder.HasOne<TaskTemplate>()
            .WithMany("_fieldDefinitions")
            .HasForeignKey(fieldDefinition => fieldDefinition.TaskTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
