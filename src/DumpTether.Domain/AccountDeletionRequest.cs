namespace DumpTether.Domain;

public enum AccountDeletionRequestState
{
    Pending = 1,
    Deleting = 2
}

public sealed class AccountDeletionRequest
{
    private AccountDeletionRequest()
    {
    }

    private AccountDeletionRequest(
        Guid id,
        Guid userId,
        DateTimeOffset requestedAt,
        DateTimeOffset reminderDueAt,
        DateTimeOffset scheduledFor)
    {
        Id = id;
        UserId = userId;
        RequestedAt = requestedAt;
        ReminderDueAt = reminderDueAt;
        ScheduledFor = scheduledFor;
        State = AccountDeletionRequestState.Pending;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset ReminderDueAt { get; private set; }

    public DateTimeOffset ScheduledFor { get; private set; }

    public DateTimeOffset? ReminderSentAt { get; private set; }

    public DateTimeOffset? ReminderClaimedAt { get; private set; }

    public AccountDeletionRequestState State { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    public static AccountDeletionRequest Create(
        Guid userId,
        DateTimeOffset requestedAt,
        DateTimeOffset reminderDueAt,
        DateTimeOffset scheduledFor)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));
        if (reminderDueAt <= requestedAt || scheduledFor <= reminderDueAt)
        {
            throw new ArgumentException("Account deletion reminder and deletion times are not valid.");
        }

        return new AccountDeletionRequest(
            Guid.NewGuid(),
            userId,
            requestedAt,
            reminderDueAt,
            scheduledFor);
    }

    public void MarkReminderSent(DateTimeOffset sentAt)
    {
        if (State != AccountDeletionRequestState.Pending)
        {
            throw new InvalidOperationException("Account deletion is already being processed.");
        }

        ReminderSentAt ??= sentAt;
        ReminderClaimedAt = null;
    }

    public void ClaimReminder(DateTimeOffset claimedAt)
    {
        if (State != AccountDeletionRequestState.Pending || ReminderSentAt is not null)
        {
            throw new InvalidOperationException("Account deletion reminder is not available.");
        }

        ReminderClaimedAt = claimedAt;
    }

    public void ReleaseReminderClaim() => ReminderClaimedAt = null;

    public void Claim(DateTimeOffset claimedAt)
    {
        if (State != AccountDeletionRequestState.Pending)
        {
            throw new InvalidOperationException("Account deletion has already been claimed.");
        }

        State = AccountDeletionRequestState.Deleting;
        ClaimedAt = claimedAt;
    }

    public void ReleaseClaim()
    {
        State = AccountDeletionRequestState.Pending;
        ClaimedAt = null;
    }
}
