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

        builder.Property(taskTemplate => taskTemplate.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(taskTemplate => taskTemplate.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore(taskTemplate => taskTemplate.FieldDefinitions);

        builder.HasIndex(taskTemplate => new
            {
                taskTemplate.WorkspaceId,
                taskTemplate.Name
            })
            .IsUnique();

        builder.HasOne<Workspace>()
            .WithMany("_taskTemplates")
            .HasForeignKey(taskTemplate => taskTemplate.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
