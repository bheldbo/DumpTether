using DumpTether.Domain;

namespace DumpTether.App.Workspaces;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
