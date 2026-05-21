using DumpTether.Domain;

namespace DumpTether.App.Views;

public interface ISavedViewRepository
{
    Task AddAsync(SavedView savedView, CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedView>> ListAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<SavedView?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<bool> AnyActiveWithNameAsync(
        Guid workspaceId,
        string name,
        Guid? excludedSavedViewId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
