using DumpTether.Domain;

namespace DumpTether.App.Projects;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> ListAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);
}
