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
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

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
        int sortOrder)
    {
        var fieldDefinition = FieldDefinition.Create(
            Id,
            key,
            label,
            type,
            isRequired,
            sortOrder);

        _fieldDefinitions.Add(fieldDefinition);
        return fieldDefinition;
    }
}
