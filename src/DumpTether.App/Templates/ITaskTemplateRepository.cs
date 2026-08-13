using DumpTether.Domain;

namespace DumpTether.App.Templates;

public interface ITaskTemplateRepository
{
    Task AddAsync(TaskTemplate taskTemplate, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskTemplate>> ListAsync(
        Guid? ownerUserId,
        CancellationToken cancellationToken);

    Task<TaskTemplate?> GetByIdAsync(
        Guid id,
        Guid? ownerUserId,
        bool trackChanges,
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, TaskTemplate>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task<bool> AnyActiveWithNameAsync(
        Guid? ownerUserId,
        string name,
        Guid? excludedTemplateId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
