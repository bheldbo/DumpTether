using System.Text.Json;
using DumpTether.App.Tasks;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class DevelopmentWorkspaceProvider : IDevelopmentWorkspaceProvider
{
    private const string DevelopmentProjectName = "Development Project";
    private const string DevelopmentWorkspaceName = "Development Workspace";
    private const string JobProjectName = "Job";
    private const string PersonalProjectName = "Personal";
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DevelopmentArchiveResolution[] DevelopmentArchiveResolutions =
    [
        new("Completed", "Work finished or captured elsewhere.", false),
        new("No Longer Needed", "The task is intentionally dropped.", true),
        new("Blocked", "The task cannot move forward right now.", true)
    ];

    private static readonly DevelopmentTaskTemplate[] DevelopmentTaskTemplates =
    [
        new(
            "Basic Task",
            [
                new("Context", FieldDefinitionType.LongText)
            ]),
        new(
            "Work Task",
            [
                new("Area", FieldDefinitionType.Select, ["Backend", "Frontend", "Data", "Docs", "DevOps"]),
                new("Priority", FieldDefinitionType.Select, ["Low", "Normal", "High"]),
                new("Due Date", FieldDefinitionType.Date)
            ]),
        new(
            "Service Desk Case",
            [
                new("Customer", FieldDefinitionType.Text),
                new("Severity", FieldDefinitionType.Select, ["Low", "Medium", "High", "Critical"]),
                new("Resolution Notes", FieldDefinitionType.LongText)
            ]),
        new(
            "Project Note",
            [
                new("Topic", FieldDefinitionType.Text),
                new("Decision", FieldDefinitionType.LongText),
                new("Follow-up Needed", FieldDefinitionType.Checkbox)
            ]),
        new(
            "Upgrade/Gotcha Note",
            [
                new("System", FieldDefinitionType.Text),
                new("Version", FieldDefinitionType.Text),
                new("Gotcha", FieldDefinitionType.LongText),
                new("Workaround", FieldDefinitionType.LongText)
            ])
    ];

    private static readonly string[] DevelopmentProjectNames =
    [
        DevelopmentProjectName,
        JobProjectName,
        PersonalProjectName
    ];

    private readonly IClock _clock;
    private readonly DumpTetherDbContext _dbContext;

    public DevelopmentWorkspaceProvider(IClock clock, DumpTetherDbContext dbContext)
    {
        _clock = clock;
        _dbContext = dbContext;
    }

    public async Task<DevelopmentWorkspaceContext> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await SeedLock.WaitAsync(cancellationToken);

        try
        {
            return await GetCurrentCoreAsync(cancellationToken);
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private async Task<DevelopmentWorkspaceContext> GetCurrentCoreAsync(CancellationToken cancellationToken)
    {
        // TEMPORARY: replace this with authenticated workspace/project selection.
        var workspace = await _dbContext.Workspaces
            .SingleOrDefaultAsync(
                candidate => candidate.Name == DevelopmentWorkspaceName,
                cancellationToken);

        if (workspace is null)
        {
            workspace = Workspace.Create(DevelopmentWorkspaceName, _clock.UtcNow);
            await _dbContext.Workspaces.AddAsync(workspace, cancellationToken);
        }

        foreach (var projectName in DevelopmentProjectNames)
        {
            var exists = await _dbContext.Projects
                .AnyAsync(
                    candidate =>
                        candidate.WorkspaceId == workspace.Id &&
                        candidate.Name == projectName,
                    cancellationToken);

            if (!exists)
            {
                await _dbContext.Projects.AddAsync(
                    Project.Create(workspace.Id, projectName, _clock.UtcNow),
                    cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var projects = await _dbContext.Projects
            .Where(candidate => candidate.WorkspaceId == workspace.Id)
            .ToDictionaryAsync(
                candidate => candidate.Name,
                cancellationToken);
        var project = projects[DevelopmentProjectName];

        foreach (var resolution in DevelopmentArchiveResolutions)
        {
            var exists = await _dbContext.ArchiveResolutions
                .AnyAsync(
                    candidate =>
                        candidate.WorkspaceId == workspace.Id &&
                        candidate.Name == resolution.Name,
                    cancellationToken);

            if (!exists)
            {
                await _dbContext.ArchiveResolutions.AddAsync(
                    ArchiveResolution.Create(
                        workspace.Id,
                        resolution.Name,
                        _clock.UtcNow,
                        resolution.Description,
                        resolution.RequiresExplanation),
                    cancellationToken);
            }
        }

        foreach (var templateDefinition in DevelopmentTaskTemplates)
        {
            var exists = await _dbContext.TaskTemplates
                .AnyAsync(
                    candidate =>
                        candidate.WorkspaceId == workspace.Id &&
                        candidate.Name == templateDefinition.Name &&
                        candidate.DeletedAt == null,
                    cancellationToken);

            if (!exists)
            {
                var taskTemplate = TaskTemplate.Create(
                    workspace.Id,
                    templateDefinition.Name,
                    _clock.UtcNow);

                foreach (var (field, index) in templateDefinition.Fields.Select((field, index) => (field, index)))
                {
                    taskTemplate.AddFieldDefinition(
                        GenerateKey(field.Name),
                        field.Name,
                        field.Type,
                        isRequired: false,
                        sortOrder: index,
                        field.Options.Count == 0
                            ? null
                            : JsonSerializer.Serialize(field.Options));
                }

                await _dbContext.TaskTemplates.AddAsync(taskTemplate, cancellationToken);
            }
        }

        await DeactivateDuplicateDevelopmentTemplatesAsync(workspace.Id, cancellationToken);
        await SeedDevelopmentSavedViewsAsync(workspace.Id, projects, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DevelopmentWorkspaceContext(workspace.Id, project.Id);
    }

    private async Task DeactivateDuplicateDevelopmentTemplatesAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        foreach (var templateName in DevelopmentTaskTemplates.Select(template => template.Name))
        {
            var matchingTemplates = await _dbContext.TaskTemplates
                .Where(template =>
                    template.WorkspaceId == workspaceId &&
                    template.Name == templateName &&
                    template.DeletedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var duplicateTemplate in matchingTemplates
                         .OrderBy(template => template.CreatedAt)
                         .ThenBy(template => template.Id)
                         .Skip(1))
            {
                duplicateTemplate.SoftDelete(_clock.UtcNow);
            }
        }
    }

    private async Task SeedDevelopmentSavedViewsAsync(
        Guid workspaceId,
        IReadOnlyDictionary<string, Project> projects,
        CancellationToken cancellationToken)
    {
        var definitions = new DevelopmentSavedView[]
        {
            new(
                "Inbox",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(Status: string.Empty),
                0),
            new(
                "All active",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(),
                1),
            new(
                "Job",
                SavedViewScope.Project,
                projects[JobProjectName].Id,
                new DevelopmentSavedViewFilter(ProjectId: projects[JobProjectName].Id),
                2),
            new(
                "Personal",
                SavedViewScope.Project,
                projects[PersonalProjectName].Id,
                new DevelopmentSavedViewFilter(ProjectId: projects[PersonalProjectName].Id),
                3),
            new(
                "Waiting",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(Status: "Waiting"),
                4),
            new(
                "Follow-up today",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(FollowUp: "Today"),
                5),
            new(
                "Follow-up this week",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(FollowUp: "ThisWeek"),
                6),
            new(
                "Not viewed in 7 days",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(NotViewedSinceDays: 7),
                7),
            new(
                "Not touched in 14 days",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(NotTouchedSinceDays: 14),
                8),
            new(
                "Archive",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(Archive: "Archived"),
                9)
        };

        foreach (var definition in definitions)
        {
            var exists = await _dbContext.SavedViews
                .AnyAsync(
                    candidate =>
                        candidate.WorkspaceId == workspaceId &&
                        candidate.Name == definition.Name &&
                        candidate.DeletedAt == null,
                    cancellationToken);

            if (exists)
            {
                continue;
            }

            var definitionJson = JsonSerializer.Serialize(
                definition.Filter,
                JsonSerializerOptions);
            var sortJson = JsonSerializer.Serialize(
                new DevelopmentSavedViewSort(),
                JsonSerializerOptions);
            var savedView = definition.Scope == SavedViewScope.Project
                ? SavedView.CreateProjectView(
                    workspaceId,
                    definition.ProjectId!.Value,
                    definition.Name,
                    definitionJson,
                    sortJson,
                    definition.SortOrder,
                    _clock.UtcNow)
                : SavedView.CreateWorkspaceView(
                    workspaceId,
                    definition.Name,
                    definitionJson,
                    sortJson,
                    definition.SortOrder,
                    _clock.UtcNow);

            await _dbContext.SavedViews.AddAsync(savedView, cancellationToken);
        }
    }

    private static string GenerateKey(string name)
    {
        var keyCharacters = name
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();

        return string.Join(
            '_',
            new string(keyCharacters)
                .Split('_', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record DevelopmentArchiveResolution(
        string Name,
        string Description,
        bool RequiresExplanation);

    private sealed record DevelopmentTaskTemplate(
        string Name,
        IReadOnlyList<DevelopmentFieldDefinition> Fields);

    private sealed record DevelopmentFieldDefinition(
        string Name,
        FieldDefinitionType Type,
        IReadOnlyList<string> Options)
    {
        public DevelopmentFieldDefinition(string name, FieldDefinitionType type)
            : this(name, type, [])
        {
        }
    }

    private sealed record DevelopmentSavedView(
        string Name,
        SavedViewScope Scope,
        Guid? ProjectId,
        DevelopmentSavedViewFilter Filter,
        int SortOrder);

    private sealed record DevelopmentSavedViewFilter(
        Guid? ProjectId = null,
        string? Status = null,
        string Archive = "Active",
        string? FollowUp = null,
        int? NotViewedSinceDays = null,
        int? NotTouchedSinceDays = null,
        string? Text = null);

    private sealed record DevelopmentSavedViewSort(
        string Field = "lastTouchedAt",
        string Direction = "desc");
}
