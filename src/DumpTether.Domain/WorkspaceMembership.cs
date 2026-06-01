namespace DumpTether.Domain;

public sealed class WorkspaceMembership
{
    private WorkspaceMembership()
    {
    }

    private WorkspaceMembership(
        Guid id,
        Guid workspaceId,
        Guid userId,
        WorkspaceMembershipRole role,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceMembershipRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkspaceMembership Create(
        Guid workspaceId,
        Guid userId,
        WorkspaceMembershipRole role,
        DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));
        DomainGuards.NotEmpty(userId, nameof(userId));

        return new WorkspaceMembership(
            Guid.NewGuid(),
            workspaceId,
            userId,
            role,
            createdAt);
    }
}
