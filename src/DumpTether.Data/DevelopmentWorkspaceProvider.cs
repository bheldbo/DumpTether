using System.Text.Json;
using DumpTether.App.Tasks;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class DevelopmentWorkspaceProvider : IDevelopmentWorkspaceProvider
{
    private const string DevelopmentProjectName = "Development Project";
    private const string DevelopmentWorkspaceName = "Development Workspace";
    private static readonly SemaphoreSlim SeedLock = new(1, 1);

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

        var project = await _dbContext.Projects
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.WorkspaceId == workspace.Id &&
                    candidate.Name == DevelopmentProjectName,
                cancellationToken);

        if (project is null)
        {
            project = Project.Create(workspace.Id, DevelopmentProjectName, _clock.UtcNow);
            await _dbContext.Projects.AddAsync(project, cancellationToken);
        }

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
}
