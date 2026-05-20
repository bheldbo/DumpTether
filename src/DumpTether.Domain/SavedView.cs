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
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        Name = name;
        Scope = scope;
        DefinitionJson = definitionJson;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public SavedViewScope Scope { get; private set; }

    public string DefinitionJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static SavedView CreateWorkspaceView(
        Guid workspaceId,
        string name,
        string definitionJson,
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
            createdAt);
    }

    public static SavedView CreateProjectView(
        Guid workspaceId,
        Guid projectId,
        string name,
        string definitionJson,
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
            createdAt);
    }
}
