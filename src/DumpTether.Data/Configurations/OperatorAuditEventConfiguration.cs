using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class OperatorAuditEventConfiguration : IEntityTypeConfiguration<OperatorAuditEvent>
{
    public void Configure(EntityTypeBuilder<OperatorAuditEvent> builder)
    {
        builder.ToTable("operator_audit_events");

        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(auditEvent => auditEvent.Actor)
            .HasColumnName("actor")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.Action)
            .HasColumnName("action")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.TargetUserId)
            .HasColumnName("target_user_id")
            .IsRequired();

        builder.Property(auditEvent => auditEvent.TargetEmail)
            .HasColumnName("target_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.HasIndex(auditEvent => auditEvent.OccurredAt);
        builder.HasIndex(auditEvent => auditEvent.TargetUserId);
    }
}
