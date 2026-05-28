using DumpTether.Domain;

namespace DumpTether.App.Workspaces;

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken);

    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
