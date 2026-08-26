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

    public TaskTemplateBuiltInKind BuiltInKind { get; private set; }

    public string HeaderLayoutJson { get; private set; } = "[]";

    public string EntryLayoutJson { get; private set; } = "[]";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsActive => DeletedAt is null;

    public bool IsProtected => BuiltInKind != TaskTemplateBuiltInKind.None;

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
        EnsureEditable();
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
        EnsureEditable();
        HeaderLayoutJson = DomainGuards.NotBlank(headerLayoutJson, nameof(headerLayoutJson));
        EntryLayoutJson = DomainGuards.NotBlank(entryLayoutJson, nameof(entryLayoutJson));
        UpdatedAt = updatedAt;
    }

    public void SoftDelete(DateTimeOffset deletedAt)
    {
        EnsureEditable();
        DeletedAt ??= deletedAt;
        UpdatedAt = deletedAt;
    }

    public void ReleaseOwnership(DateTimeOffset updatedAt)
    {
        OwnerUserId = null;
        UpdatedAt = updatedAt;
    }

    public void MarkAsBuiltIn(TaskTemplateBuiltInKind kind, DateTimeOffset updatedAt)
    {
        if (kind == TaskTemplateBuiltInKind.None || !Enum.IsDefined(kind))
        {
            throw new ArgumentException("A supported built-in template kind is required.", nameof(kind));
        }

        if (BuiltInKind != TaskTemplateBuiltInKind.None && BuiltInKind != kind)
        {
            throw new InvalidOperationException("A built-in template kind cannot be changed.");
        }

        BuiltInKind = kind;
        UpdatedAt = updatedAt;
    }

    public void RestoreBuiltInDefinition(
        TaskTemplateBuiltInKind kind,
        string name,
        string headerLayoutJson,
        string entryLayoutJson,
        DateTimeOffset updatedAt)
    {
        if (kind == TaskTemplateBuiltInKind.None || !Enum.IsDefined(kind))
        {
            throw new ArgumentException("A supported built-in template kind is required.", nameof(kind));
        }

        if (BuiltInKind != TaskTemplateBuiltInKind.None && BuiltInKind != kind)
        {
            throw new InvalidOperationException("A built-in template kind cannot be changed.");
        }

        BuiltInKind = kind;
        Name = DomainGuards.NotBlank(name, nameof(name));
        HeaderLayoutJson = DomainGuards.NotBlank(headerLayoutJson, nameof(headerLayoutJson));
        EntryLayoutJson = DomainGuards.NotBlank(entryLayoutJson, nameof(entryLayoutJson));
        UpdatedAt = updatedAt;
    }

    public void RetireBuiltIn(TaskTemplateBuiltInKind kind, DateTimeOffset retiredAt)
    {
        if (kind == TaskTemplateBuiltInKind.None || BuiltInKind != kind)
        {
            throw new InvalidOperationException("Only the matching built-in template can be retired.");
        }

        BuiltInKind = TaskTemplateBuiltInKind.None;
        DeletedAt ??= retiredAt;
        UpdatedAt = retiredAt;
    }

    private void EnsureEditable()
    {
        if (IsProtected)
        {
            throw new InvalidOperationException("Built-in task templates cannot be changed or deleted.");
        }
    }
}
