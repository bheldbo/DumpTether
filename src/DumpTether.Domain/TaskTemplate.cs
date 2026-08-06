namespace DumpTether.Domain;

public sealed class TaskTemplate
{
    private readonly List<FieldDefinition> _fieldDefinitions = [];

    private TaskTemplate()
    {
    }

    private TaskTemplate(Guid id, Guid? ownerUserId, string name, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
        HeaderLayoutJson = "[]";
        EntryLayoutJson = "[]";
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string HeaderLayoutJson { get; private set; } = "[]";

    public string EntryLayoutJson { get; private set; } = "[]";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsActive => DeletedAt is null;

    public IReadOnlyCollection<FieldDefinition> FieldDefinitions => _fieldDefinitions.AsReadOnly();

    public static TaskTemplate Create(Guid? ownerUserId, string name, DateTimeOffset createdAt)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Owner user id cannot be empty.", nameof(ownerUserId));
        }

        return new TaskTemplate(
            Guid.NewGuid(),
            ownerUserId,
            DomainGuards.NotBlank(name, nameof(name)),
            createdAt);
    }

    public FieldDefinition AddFieldDefinition(
        string key,
        string label,
        FieldDefinitionType type,
        FieldDefinitionScope scope,
        bool isRequired,
        int sortOrder,
        string? optionsJson = null,
        int layoutRow = 1,
        int layoutColumn = 1,
        int layoutRowSpan = 1,
        int layoutColumnSpan = 1,
        double layoutWeight = 1)
    {
        var fieldDefinition = FieldDefinition.Create(
            Id,
            key,
            label,
            type,
            scope,
            isRequired,
            sortOrder,
            optionsJson,
            layoutRow,
            layoutColumn,
            layoutRowSpan,
            layoutColumnSpan,
            layoutWeight);

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

    public void UpdateLayout(
        string headerLayoutJson,
        string entryLayoutJson,
        DateTimeOffset updatedAt)
    {
        HeaderLayoutJson = DomainGuards.NotBlank(headerLayoutJson, nameof(headerLayoutJson));
        EntryLayoutJson = DomainGuards.NotBlank(entryLayoutJson, nameof(entryLayoutJson));
        UpdatedAt = updatedAt;
    }

    public void SoftDelete(DateTimeOffset deletedAt)
    {
        DeletedAt ??= deletedAt;
        UpdatedAt = deletedAt;
    }

    public void ReleaseOwnership(DateTimeOffset updatedAt)
    {
        OwnerUserId = null;
        UpdatedAt = updatedAt;
    }
}
