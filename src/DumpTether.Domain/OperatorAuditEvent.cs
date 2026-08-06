namespace DumpTether.Domain;

public sealed class OperatorAuditEvent
{
    private OperatorAuditEvent()
    {
    }

    private OperatorAuditEvent(
        Guid id,
        string actor,
        string action,
        Guid targetUserId,
        string targetEmail,
        string reason,
        DateTimeOffset occurredAt)
    {
        Id = id;
        Actor = actor;
        Action = action;
        TargetUserId = targetUserId;
        TargetEmail = targetEmail;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public string Actor { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public Guid TargetUserId { get; private set; }

    public string TargetEmail { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public static OperatorAuditEvent Create(
        string actor,
        string action,
        Guid targetUserId,
        string targetEmail,
        string reason,
        DateTimeOffset occurredAt)
    {
        DomainGuards.NotEmpty(targetUserId, nameof(targetUserId));

        return new OperatorAuditEvent(
            Guid.NewGuid(),
            Truncate(DomainGuards.NotBlank(actor, nameof(actor)), 160),
            Truncate(DomainGuards.NotBlank(action, nameof(action)), 120),
            targetUserId,
            Truncate(DomainGuards.NotBlank(targetEmail, nameof(targetEmail)), 320),
            Truncate(DomainGuards.NotBlank(reason, nameof(reason)), 1000),
            occurredAt);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
