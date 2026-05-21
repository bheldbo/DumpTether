namespace DumpTether.Domain;

public sealed class TaskTemplate
{
    private readonly List<FieldDefinition> _fieldDefinitions = [];

    private TaskTemplate()
    {
    }

    private TaskTemplate(Guid id, Guid workspaceId, string name, DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsActive => DeletedAt is null;

    public IReadOnlyCollection<FieldDefinition> FieldDefinitions => _fieldDefinitions.AsReadOnly();

    public static TaskTemplate Create(Guid workspaceId, string name, DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));

        return new TaskTemplate(
            Guid.NewGuid(),
            workspaceId,
            DomainGuards.NotBlank(name, nameof(name)),
            createdAt);
    }

    public FieldDefinition AddFieldDefinition(
        string key,
        string label,
        FieldDefinitionType type,
        bool isRequired,
        int sortOrder,
        string? optionsJson = null)
    {
        var fieldDefinition = FieldDefinition.Create(
            Id,
            key,
            label,
            type,
            isRequired,
            sortOrder,
            optionsJson);

        _fieldDefinitions.Add(fieldDefinition);
        return fieldDefinition;
    }

    public void Rename(string name, DateTimeOffset updatedAt)
    {
        var normalizedName = DomainGuards.NotBlank(name, nameof(name));

        if (Name == normalizedName)
        {
            return;
        }

        Name = normalizedName;
        UpdatedAt = updatedAt;
    }

    public void MarkUpdated(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
    }

    public void SoftDelete(DateTimeOffset deletedAt)
    {
        DeletedAt ??= deletedAt;
        UpdatedAt = deletedAt;
    }
}
