namespace DumpTether.Domain;

public sealed class Project
{
    private readonly List<TaskItem> _taskItems = [];

    private Project()
    {
    }

    private Project(Guid id, Guid workspaceId, string name, DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Color { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<TaskItem> TaskItems => _taskItems.AsReadOnly();

    public static Project Create(Guid workspaceId, string name, DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));

        return new Project(
            Guid.NewGuid(),
            workspaceId,
            DomainGuards.NotBlank(name, nameof(name)),
            createdAt);
    }

    public void Rename(string name)
    {
        Name = DomainGuards.NotBlank(name, nameof(name));
    }

    public void ChangeColor(string? color)
    {
        Color = DomainGuards.OptionalHexColor(color, nameof(color));
    }

    public TaskItem AddTaskItem(string title, DateTimeOffset createdAt, Guid? taskTemplateId = null)
    {
        var taskItem = TaskItem.Create(WorkspaceId, Id, title, createdAt, taskTemplateId);
        _taskItems.Add(taskItem);

        return taskItem;
    }
}
