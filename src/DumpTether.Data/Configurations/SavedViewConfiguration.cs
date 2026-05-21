using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("saved_views", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_saved_views_scope_project",
                "(scope = 'Workspace' AND project_id IS NULL) OR (scope = 'Project' AND project_id IS NOT NULL)");
        });

        builder.HasKey(savedView => savedView.Id);

        builder.Property(savedView => savedView.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(savedView => savedView.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(savedView => savedView.ProjectId)
            .HasColumnName("project_id");

        builder.Property(savedView => savedView.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(savedView => savedView.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(savedView => savedView.DefinitionJson)
            .HasColumnName("definition")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(savedView => savedView.SortJson)
            .HasColumnName("sort")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(savedView => savedView.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(savedView => savedView.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(savedView => savedView.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(savedView => savedView.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(savedView => new
            {
                savedView.WorkspaceId,
                savedView.Name
            })
            .IsUnique()
            .HasFilter("project_id IS NULL AND deleted_at IS NULL");

        builder.HasIndex(savedView => new
            {
                savedView.WorkspaceId,
                savedView.ProjectId,
                savedView.Name
            })
            .IsUnique()
            .HasFilter("project_id IS NOT NULL AND deleted_at IS NULL");

        builder.HasOne<Workspace>()
            .WithMany("_savedViews")
            .HasForeignKey(savedView => savedView.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(savedView => savedView.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
