using DumpTether.Domain;

namespace DumpTether.App.Tasks;

public interface ITaskItemRepository
{
    Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken);

    Task<TaskItem?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, FieldDefinition>> GetFieldDefinitionsAsync(
        IEnumerable<Guid> fieldDefinitionIds,
        CancellationToken cancellationToken);

    Task<ArchiveResolution?> GetArchiveResolutionByIdAsync(
        Guid id,
        Guid workspaceId,
        CancellationToken cancellationToken);


    Task SaveChangesAsync(CancellationToken cancellationToken);
}
