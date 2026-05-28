using DumpTether.Domain;

namespace DumpTether.App.Projects;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> ListAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task AddAsync(Project project, CancellationToken cancellationToken);

    Task<Project?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
