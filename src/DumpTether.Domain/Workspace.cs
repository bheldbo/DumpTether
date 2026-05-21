namespace DumpTether.Domain;

public sealed class Workspace
{
    private readonly List<ArchiveResolution> _archiveResolutions = [];
    private readonly List<Project> _projects = [];
    private readonly List<SavedView> _savedViews = [];
    private readonly List<TaskTemplate> _taskTemplates = [];

    private Workspace()
    {
    }

    private Workspace(Guid id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    public IReadOnlyCollection<TaskTemplate> TaskTemplates => _taskTemplates.AsReadOnly();

    public IReadOnlyCollection<ArchiveResolution> ArchiveResolutions => _archiveResolutions.AsReadOnly();

    public IReadOnlyCollection<SavedView> SavedViews => _savedViews.AsReadOnly();

    public static Workspace Create(string name, DateTimeOffset createdAt)
    {
        return new Workspace(
            Guid.NewGuid(),
            DomainGuards.NotBlank(name, nameof(name)),
            createdAt);
    }

    public Project AddProject(string name, DateTimeOffset createdAt)
    {
        var project = Project.Create(Id, name, createdAt);
        _projects.Add(project);

        return project;
    }

    public TaskTemplate AddTaskTemplate(string name, DateTimeOffset createdAt)
    {
        var taskTemplate = TaskTemplate.Create(Id, name, createdAt);
        _taskTemplates.Add(taskTemplate);

        return taskTemplate;
    }

    public ArchiveResolution AddArchiveResolution(
        string name,
        DateTimeOffset createdAt,
        string? description = null,
        bool requiresExplanation = false)
    {
        var archiveResolution = ArchiveResolution.Create(
            Id,
            name,
            createdAt,
            description,
            requiresExplanation);

        _archiveResolutions.Add(archiveResolution);

        return archiveResolution;
    }

    public SavedView AddWorkspaceView(
        string name,
        string definitionJson,
        DateTimeOffset createdAt,
        string sortJson = "{}",
        int sortOrder = 0)
    {
        var savedView = SavedView.CreateWorkspaceView(
            Id,
            name,
            definitionJson,
            sortJson,
            sortOrder,
            createdAt);
        _savedViews.Add(savedView);

        return savedView;
    }

    public SavedView AddProjectView(
        Project project,
        string name,
        string definitionJson,
        DateTimeOffset createdAt,
        string sortJson = "{}",
        int sortOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.WorkspaceId != Id)
        {
            throw new InvalidOperationException("Project must belong to this workspace.");
        }

        var savedView = SavedView.CreateProjectView(
            Id,
            project.Id,
            name,
            definitionJson,
            sortJson,
            sortOrder,
            createdAt);

        _savedViews.Add(savedView);
        return savedView;
    }
}
