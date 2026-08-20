using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");

        builder.HasKey(workspace => workspace.Id);

        builder.Property(workspace => workspace.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(workspace => workspace.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(workspace => workspace.Color)
            .HasColumnName("color")
            .HasMaxLength(7);

        builder.Property(workspace => workspace.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(workspace => workspace.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(workspace => workspace.Projects);
        builder.Ignore(workspace => workspace.SavedViews);
        builder.Ignore(workspace => workspace.Memberships);

        builder.HasIndex(workspace => workspace.Name);
    }
}
