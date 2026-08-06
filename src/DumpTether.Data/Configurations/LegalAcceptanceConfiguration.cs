using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> builder)
    {
        builder.ToTable("legal_acceptances");

        builder.HasKey(acceptance => acceptance.Id);

        builder.Property(acceptance => acceptance.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(acceptance => acceptance.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(acceptance => acceptance.DocumentType)
            .HasColumnName("document_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(acceptance => acceptance.DocumentVersion)
            .HasColumnName("document_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(acceptance => acceptance.AcceptedAt)
            .HasColumnName("accepted_at")
            .IsRequired();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(acceptance => acceptance.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(acceptance => new
            {
                acceptance.UserId,
                acceptance.DocumentType,
                acceptance.DocumentVersion
            })
            .IsUnique();
    }
}
