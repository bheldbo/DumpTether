using System.Text.Json;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DumpTether.Data;

internal sealed class DevelopmentWorkspaceProvider : IDevelopmentWorkspaceProvider
{
    private const string DefaultProjectName = "General";
    private const string DevelopmentWorkspaceName = "All Tasks";
    private const string LegacyDevelopmentProjectName = "Development Project";
    private const string LegacyDevelopmentWorkspaceName = "Development Workspace";
    private const string GeneralProjectName = "General";
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
            "ToDo Task",
            [
                new("Done", FieldDefinitionType.Checkbox),
                new("Next step", FieldDefinitionType.Text)
            ])
    ];

    private static readonly string[] LegacyDevelopmentTaskTemplateNames =
    [
        "Work Task",
        "Service Desk Case",
        "Project Note",
        "Upgrade/Gotcha Note"
    ];

    private static readonly string[] DevelopmentProjectNames =
    [
        DefaultProjectName
    ];

    private static readonly string[] LegacyDevelopmentSavedViewNames =
    [
        "Inbox",
        "All active",
        "Job",
        "Personal",
        "Waiting",
        "Follow-up today",
        "Follow-up this week",
        "Not viewed in 7 days",
        "Not touched in 14 days"
    ];

    private readonly IClock _clock;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly ICurrentWorkspaceSelection _currentWorkspaceSelection;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly DumpTetherDbContext _dbContext;

    public DevelopmentWorkspaceProvider(
        IClock clock,
        IOptions<AuthOptions> authOptions,
        ICurrentWorkspaceSelection currentWorkspaceSelection,
        ICurrentUserSessionProvider currentUserSessionProvider,
        DumpTetherDbContext dbContext)
    {
        _clock = clock;
        _authOptions = authOptions;
        _currentWorkspaceSelection = currentWorkspaceSelection;
        _currentUserSessionProvider = currentUserSessionProvider;
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
        // TEMPORARY: anonymous development mode still creates a local workspace until auth is required.
        // Authenticated requests are scoped to workspace membership.
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (currentSession is null && _authOptions.Value.RequireAuthentication)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var selectedWorkspace = await GetSelectedWorkspaceAsync(
            currentSession?.UserId,
            currentSession is null ? null : AppUser.NormalizeEmail(currentSession.Email),
            cancellationToken);
        var workspace = selectedWorkspace?.Workspace;
        var isSharedOnly = selectedWorkspace?.IsSharedOnly ?? false;
        var membershipRole = selectedWorkspace?.MembershipRole;

        if (workspace is null)
        {
            workspace = Workspace.Create(DevelopmentWorkspaceName, _clock.UtcNow);
            await _dbContext.Workspaces.AddAsync(workspace, cancellationToken);

            if (currentSession is not null)
            {
                await _dbContext.WorkspaceMemberships.AddAsync(
                    WorkspaceMembership.Create(
                        workspace.Id,
                        currentSession.UserId,
                        WorkspaceMembershipRole.Owner,
                        _clock.UtcNow),
                    cancellationToken);
            }
        }

        if (string.Equals(
                workspace.Name,
                LegacyDevelopmentWorkspaceName,
                StringComparison.OrdinalIgnoreCase))
        {
            workspace.Rename(DevelopmentWorkspaceName, _clock.UtcNow);
        }

        var legacyDefaultProject = await _dbContext.Projects
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.WorkspaceId == workspace.Id &&
                    candidate.Name == LegacyDevelopmentProjectName,
                cancellationToken);
        var generalProjectExists = await _dbContext.Projects
            .AnyAsync(
                candidate =>
                    candidate.WorkspaceId == workspace.Id &&
                    candidate.Name == GeneralProjectName,
                cancellationToken);

        if (legacyDefaultProject is not null && !generalProjectExists)
        {
            legacyDefaultProject.Rename(GeneralProjectName);
            generalProjectExists = true;
        }

        var defaultProjectName = GeneralProjectName;
        var seedProjectNames = DevelopmentProjectNames;

        foreach (var projectName in seedProjectNames)
        {
            var exists =
                projectName == GeneralProjectName && generalProjectExists ||
                await _dbContext.Projects
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
        var project = projects.TryGetValue(defaultProjectName, out var defaultProject)
            ? defaultProject
            : projects.Values.OrderBy(candidate => candidate.Name).First();

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

        var templateOwnerUserId = currentSession?.UserId;

        foreach (var templateDefinition in DevelopmentTaskTemplates)
        {
            var exists = await _dbContext.TaskTemplates
                .AnyAsync(
                    candidate =>
                        candidate.OwnerUserId == templateOwnerUserId &&
                        candidate.Name == templateDefinition.Name &&
                        candidate.DeletedAt == null,
                    cancellationToken);

            if (!exists)
            {
                var taskTemplate = TaskTemplate.Create(
                    templateOwnerUserId,
                    templateDefinition.Name,
                    _clock.UtcNow);

                foreach (var (field, index) in templateDefinition.Fields.Select((field, index) => (field, index)))
                {
                    taskTemplate.AddFieldDefinition(
                        GenerateKey(field.Name),
                        field.Name,
                        field.Type,
                        FieldDefinitionScope.Header,
                        isRequired: false,
                        sortOrder: index,
                        optionsJson: field.Options.Count == 0
                            ? null
                            : JsonSerializer.Serialize(field.Options),
                        layoutRow: field.LayoutRow,
                        layoutColumn: field.LayoutColumn,
                        layoutRowSpan: field.LayoutRowSpan,
                        layoutColumnSpan: field.LayoutColumnSpan);
                }

                await _dbContext.TaskTemplates.AddAsync(taskTemplate, cancellationToken);
            }
        }

        await DeactivateDuplicateDevelopmentTemplatesAsync(templateOwnerUserId, cancellationToken);
        await DeactivateLegacyDevelopmentTemplatesAsync(templateOwnerUserId, cancellationToken);
        await SeedDevelopmentSavedViewsAsync(workspace.Id, projects, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DevelopmentWorkspaceContext(workspace.Id, project.Id, isSharedOnly, membershipRole);
    }

    private async Task<SelectedWorkspace?> GetSelectedWorkspaceAsync(
        Guid? currentUserId,
        string? currentNormalizedEmail,
        CancellationToken cancellationToken)
    {
        if (currentUserId.HasValue)
        {
            if (_currentWorkspaceSelection.WorkspaceId.HasValue)
            {
                var selectedWorkspaceId = _currentWorkspaceSelection.WorkspaceId.Value;
                var selectedWorkspace = await _dbContext.WorkspaceMemberships
                    .Where(membership =>
                        membership.UserId == currentUserId.Value &&
                        membership.WorkspaceId == selectedWorkspaceId)
                    .Join(
                        _dbContext.Workspaces,
                        membership => membership.WorkspaceId,
                        workspace => workspace.Id,
                        (membership, workspace) => new { membership, workspace })
                    .SingleOrDefaultAsync(cancellationToken);

                if (selectedWorkspace is not null)
                {
                    return new SelectedWorkspace(
                        selectedWorkspace.workspace,
                        IsSharedOnly: false,
                        selectedWorkspace.membership.Role);
                }

                var hasSelectedSharedWorkspaceAccess = await _dbContext.TaskItemShares
                    .AnyAsync(share =>
                        (share.SharedWithUserId == currentUserId.Value ||
                            share.NormalizedEmail == currentNormalizedEmail) &&
                        share.WorkspaceId == selectedWorkspaceId &&
                        share.RevokedAt == null &&
                        (share.AcceptedAt != null || share.TokenHash == null),
                        cancellationToken);

                if (hasSelectedSharedWorkspaceAccess)
                {
                    var selectedSharedWorkspace = await _dbContext.Workspaces
                        .SingleOrDefaultAsync(
                            workspace => workspace.Id == selectedWorkspaceId,
                            cancellationToken);

                    if (selectedSharedWorkspace is not null)
                    {
                        return new SelectedWorkspace(
                            selectedSharedWorkspace,
                            IsSharedOnly: true,
                            MembershipRole: null);
                    }
                }
            }

            var userWorkspaces = await _dbContext.WorkspaceMemberships
                .Where(membership => membership.UserId == currentUserId.Value)
                .Join(
                    _dbContext.Workspaces,
                    membership => membership.WorkspaceId,
                    workspace => workspace.Id,
                    (membership, workspace) => new { membership, workspace })
                .ToListAsync(cancellationToken);

            var firstWorkspace = userWorkspaces
                .OrderBy(item => item.workspace.CreatedAt)
                .FirstOrDefault();

            if (firstWorkspace is not null)
            {
                return new SelectedWorkspace(
                    firstWorkspace.workspace,
                    IsSharedOnly: false,
                    firstWorkspace.membership.Role);
            }

            var sharedWorkspace = await _dbContext.TaskItemShares
                .Where(share =>
                    (share.SharedWithUserId == currentUserId.Value ||
                        share.NormalizedEmail == currentNormalizedEmail) &&
                    share.RevokedAt == null &&
                    (share.AcceptedAt != null || share.TokenHash == null))
                .Join(
                    _dbContext.Workspaces,
                    share => share.WorkspaceId,
                    workspace => workspace.Id,
                    (_, workspace) => workspace)
                .OrderBy(workspace => workspace.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return sharedWorkspace is null
                ? null
                : new SelectedWorkspace(sharedWorkspace, IsSharedOnly: true, MembershipRole: null);
        }

        if (_currentWorkspaceSelection.WorkspaceId.HasValue)
        {
            var selectedWorkspace = await _dbContext.Workspaces
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == _currentWorkspaceSelection.WorkspaceId.Value,
                    cancellationToken);

            if (selectedWorkspace is not null)
            {
                return new SelectedWorkspace(selectedWorkspace, IsSharedOnly: false, MembershipRole: null);
            }
        }

        var developmentWorkspace = await _dbContext.Workspaces
            .Where(candidate =>
                candidate.Name == DevelopmentWorkspaceName ||
                candidate.Name == LegacyDevelopmentWorkspaceName)
            .OrderBy(candidate => candidate.Name == DevelopmentWorkspaceName ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken);

        return developmentWorkspace is null
            ? null
            : new SelectedWorkspace(developmentWorkspace, IsSharedOnly: false, MembershipRole: null);
    }

    private async Task DeactivateDuplicateDevelopmentTemplatesAsync(
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        foreach (var templateName in DevelopmentTaskTemplates.Select(template => template.Name))
        {
            var matchingTemplates = await _dbContext.TaskTemplates
                .Where(template =>
                    template.OwnerUserId == ownerUserId &&
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

    private async Task DeactivateLegacyDevelopmentTemplatesAsync(
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        var legacyTemplates = await _dbContext.TaskTemplates
            .Where(template =>
                template.OwnerUserId == ownerUserId &&
                template.DeletedAt == null &&
                LegacyDevelopmentTaskTemplateNames.Contains(template.Name))
            .ToListAsync(cancellationToken);

        foreach (var legacyTemplate in legacyTemplates)
        {
            legacyTemplate.SoftDelete(_clock.UtcNow);
        }
    }

    private async Task SeedDevelopmentSavedViewsAsync(
        Guid workspaceId,
        IReadOnlyDictionary<string, Project> projects,
        CancellationToken cancellationToken)
    {
        var allTasksExists = await _dbContext.SavedViews
            .AnyAsync(
                savedView =>
                    savedView.WorkspaceId == workspaceId &&
                    savedView.DeletedAt == null &&
                    savedView.Name == "All Tasks",
                cancellationToken);

        if (!allTasksExists)
        {
            var legacyViews = await _dbContext.SavedViews
                .Where(savedView =>
                    savedView.WorkspaceId == workspaceId &&
                    savedView.DeletedAt == null &&
                    (LegacyDevelopmentSavedViewNames.Contains(savedView.Name) ||
                        savedView.Name == "Overview"))
                .ToListAsync(cancellationToken);

            foreach (var legacyView in legacyViews)
            {
                legacyView.SoftDelete(_clock.UtcNow);
            }
        }

        var definitions = new DevelopmentSavedView[]
        {
            new(
                "All Tasks",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(),
                0),
            new(
                "Archive",
                SavedViewScope.Workspace,
                null,
                new DevelopmentSavedViewFilter(Archive: "Archived"),
                1)
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
        IReadOnlyList<string> Options,
        int LayoutRow,
        int LayoutColumn,
        int LayoutRowSpan,
        int LayoutColumnSpan)
    {
        public DevelopmentFieldDefinition(string name, FieldDefinitionType type)
            : this(
                name,
                type,
                [],
                1,
                1,
                1,
                type == FieldDefinitionType.LongText ? 2 : 1)
        {
        }

        public DevelopmentFieldDefinition(
            string name,
            FieldDefinitionType type,
            IReadOnlyList<string> options)
            : this(
                name,
                type,
                options,
                1,
                1,
                1,
                type == FieldDefinitionType.LongText ? 2 : 1)
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

    private sealed record SelectedWorkspace(
        Workspace Workspace,
        bool IsSharedOnly,
        WorkspaceMembershipRole? MembershipRole);
}
