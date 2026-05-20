namespace DumpTether.Domain;

public sealed class ArchiveResolution
{
    private ArchiveResolution()
    {
    }

    private ArchiveResolution(
        Guid id,
        Guid workspaceId,
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        Description = description;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ArchiveResolution Create(
        Guid workspaceId,
        string name,
        DateTimeOffset createdAt,
        string? description = null)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));

        return new ArchiveResolution(
            Guid.NewGuid(),
            workspaceId,
            DomainGuards.NotBlank(name, nameof(name)),
            DomainGuards.OptionalTrimmed(description),
            createdAt);
    }

    public void Rename(string name)
    {
        Name = DomainGuards.NotBlank(name, nameof(name));
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
