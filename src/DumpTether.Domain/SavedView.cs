namespace DumpTether.Domain;

public sealed class SavedView
{
    private SavedView()
    {
    }

    private SavedView(
        Guid id,
        Guid workspaceId,
        Guid? projectId,
        string name,
        SavedViewScope scope,
        string definitionJson,
        string sortJson,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        Name = name;
        Scope = scope;
        DefinitionJson = definitionJson;
        SortJson = sortJson;
        SortOrder = sortOrder;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public SavedViewScope Scope { get; private set; }

    public string DefinitionJson { get; private set; } = string.Empty;

    public string SortJson { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static SavedView CreateWorkspaceView(
        Guid workspaceId,
        string name,
        string definitionJson,
        string sortJson,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));

        return new SavedView(
            Guid.NewGuid(),
            workspaceId,
            null,
            DomainGuards.NotBlank(name, nameof(name)),
            SavedViewScope.Workspace,
            DomainGuards.NotBlank(definitionJson, nameof(definitionJson)),
            DomainGuards.NotBlank(sortJson, nameof(sortJson)),
            sortOrder,
            createdAt);
    }

    public static SavedView CreateProjectView(
        Guid workspaceId,
        Guid projectId,
        string name,
        string definitionJson,
        string sortJson,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));
        DomainGuards.NotEmpty(projectId, nameof(projectId));

        return new SavedView(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            DomainGuards.NotBlank(name, nameof(name)),
            SavedViewScope.Project,
            DomainGuards.NotBlank(definitionJson, nameof(definitionJson)),
            DomainGuards.NotBlank(sortJson, nameof(sortJson)),
            sortOrder,
            createdAt);
    }

    public void UpdateWorkspaceView(
        string name,
        string definitionJson,
        string sortJson,
        int sortOrder,
        DateTimeOffset updatedAt)
    {
        UpdateCore(
            null,
            DomainGuards.NotBlank(name, nameof(name)),
            SavedViewScope.Workspace,
            DomainGuards.NotBlank(definitionJson, nameof(definitionJson)),
            DomainGuards.NotBlank(sortJson, nameof(sortJson)),
            sortOrder,
            updatedAt);
    }

    public void UpdateProjectView(
        Guid projectId,
        string name,
        string definitionJson,
        string sortJson,
        int sortOrder,
        DateTimeOffset updatedAt)
    {
        DomainGuards.NotEmpty(projectId, nameof(projectId));

        UpdateCore(
            projectId,
            DomainGuards.NotBlank(name, nameof(name)),
            SavedViewScope.Project,
            DomainGuards.NotBlank(definitionJson, nameof(definitionJson)),
            DomainGuards.NotBlank(sortJson, nameof(sortJson)),
            sortOrder,
            updatedAt);
    }

    public void SoftDelete(DateTimeOffset deletedAt)
    {
        if (DeletedAt.HasValue)
        {
            return;
        }

        DeletedAt = deletedAt;
        UpdatedAt = deletedAt;
    }

    private void UpdateCore(
        Guid? projectId,
        string name,
        SavedViewScope scope,
        string definitionJson,
        string sortJson,
        int sortOrder,
        DateTimeOffset updatedAt)
    {
        ProjectId = projectId;
        Name = name;
        Scope = scope;
        DefinitionJson = definitionJson;
        SortJson = sortJson;
        SortOrder = sortOrder;
        UpdatedAt = updatedAt;
    }
}
