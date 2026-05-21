using DumpTether.Domain;

namespace DumpTether.App.Templates;

public interface ITaskTemplateRepository
{
    Task AddAsync(TaskTemplate taskTemplate, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskTemplate>> ListAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<TaskTemplate?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        bool trackChanges,
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<bool> AnyActiveWithNameAsync(
        Guid workspaceId,
        string name,
        Guid? excludedTemplateId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
