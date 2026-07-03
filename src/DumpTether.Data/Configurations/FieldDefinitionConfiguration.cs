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

        builder.Property(fieldDefinition => fieldDefinition.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(40)
            .HasSentinel((FieldDefinitionScope)0)
            .HasDefaultValue(FieldDefinitionScope.Header)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.IsRequired)
            .HasColumnName("is_required")
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.LayoutRow)
            .HasColumnName("layout_row")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.LayoutColumn)
            .HasColumnName("layout_column")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.LayoutRowSpan)
            .HasColumnName("layout_row_span")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.LayoutColumnSpan)
            .HasColumnName("layout_column_span")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.LayoutWeight)
            .HasColumnName("layout_weight")
            .HasDefaultValue(1.0)
            .IsRequired();

        builder.Property(fieldDefinition => fieldDefinition.OptionsJson)
            .HasColumnName("options")
            .HasColumnType("jsonb");

        builder.Property(fieldDefinition => fieldDefinition.DeactivatedAt)
            .HasColumnName("deactivated_at");

        builder.Ignore(fieldDefinition => fieldDefinition.IsActive);

        builder.HasIndex(fieldDefinition => new
            {
                fieldDefinition.TaskTemplateId,
                fieldDefinition.Scope,
                fieldDefinition.Key
            });

        builder.HasIndex(fieldDefinition => new
        {
            fieldDefinition.TaskTemplateId,
            fieldDefinition.Scope,
            fieldDefinition.SortOrder
        });

        builder.HasOne<TaskTemplate>()
            .WithMany("_fieldDefinitions")
            .HasForeignKey(fieldDefinition => fieldDefinition.TaskTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
