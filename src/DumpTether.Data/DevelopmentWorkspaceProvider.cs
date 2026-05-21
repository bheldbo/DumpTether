using DumpTether.App.Tasks;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class DevelopmentWorkspaceProvider : IDevelopmentWorkspaceProvider
{
    private const string DevelopmentProjectName = "Development Project";
    private const string DevelopmentWorkspaceName = "Development Workspace";

    private static readonly DevelopmentArchiveResolution[] DevelopmentArchiveResolutions =
    [
        new("Completed", "Work finished or captured elsewhere.", false),
        new("No Longer Needed", "The task is intentionally dropped.", true),
        new("Blocked", "The task cannot move forward right now.", true)
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

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DevelopmentWorkspaceContext(workspace.Id, project.Id);
    }

    private sealed record DevelopmentArchiveResolution(
        string Name,
        string Description,
        bool RequiresExplanation);
}
