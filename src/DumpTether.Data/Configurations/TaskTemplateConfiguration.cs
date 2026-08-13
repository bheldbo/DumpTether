using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class TaskTemplateConfiguration : IEntityTypeConfiguration<TaskTemplate>
{
    public void Configure(EntityTypeBuilder<TaskTemplate> builder)
    {
        builder.ToTable("task_templates");

        builder.HasKey(taskTemplate => taskTemplate.Id);

        builder.Property(taskTemplate => taskTemplate.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(taskTemplate => taskTemplate.OwnerUserId)
            .HasColumnName("owner_user_id");

        builder.Property(taskTemplate => taskTemplate.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.BuiltInKind)
            .HasColumnName("built_in_kind")
            .HasConversion<int>()
            .HasDefaultValue(TaskTemplateBuiltInKind.None)
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.HeaderLayoutJson)
            .HasColumnName("header_layout_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.EntryLayoutJson)
            .HasColumnName("entry_layout_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(taskTemplate => taskTemplate.FieldDefinitions);
        builder.Ignore(taskTemplate => taskTemplate.IsActive);
        builder.Ignore(taskTemplate => taskTemplate.IsProtected);

        builder.HasIndex(taskTemplate => new
            {
                taskTemplate.OwnerUserId,
                taskTemplate.Name
            });

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(taskTemplate => taskTemplate.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
