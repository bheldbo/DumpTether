using DumpTether.Domain;

namespace DumpTether.App.Tasks;

public interface ITaskItemRepository
{
    Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        TaskItemListScope scope,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListAsync(
        TaskItemQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> CountByQueriesAsync(
        IReadOnlyDictionary<Guid, TaskItemQuery> queries,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListByProjectAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListByCategoryAsync(
        Guid workspaceId,
        string category,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        Guid workspaceId,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<TaskItem?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        Guid? projectId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<TaskItem?> GetByShareIdAsync(
        Guid shareId,
        Guid userId,
        string normalizedEmail,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListByWorkspaceSharesForUserAsync(
        Guid workspaceId,
        Guid userId,
        string normalizedEmail,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListByIdsAsync(
        Guid workspaceId,
        IReadOnlyList<Guid> ids,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<int> DeleteArchivedAsync(
        Guid workspaceId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListByShareTokenHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskShareInboxItem>> ListIncomingSharesAsync(
        Guid userId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, FieldDefinition>> GetFieldDefinitionsAsync(
        IEnumerable<Guid> fieldDefinitionIds,
        CancellationToken cancellationToken);

    Task<TaskTemplate?> GetTaskTemplateByIdAsync(
        Guid id,
        Guid workspaceId,
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<TaskTemplate?> GetDefaultTaskTemplateAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<ArchiveResolution?> GetArchiveResolutionByIdAsync(
        Guid id,
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
