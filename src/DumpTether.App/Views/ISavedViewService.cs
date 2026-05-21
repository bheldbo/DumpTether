namespace DumpTether.App.Views;

public interface ISavedViewService
{
    Task<IReadOnlyList<SavedViewResponse>> ListAsync(CancellationToken cancellationToken);

    Task<SavedViewResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<SavedViewResponse> CreateAsync(
        CreateSavedViewRequest request,
        CancellationToken cancellationToken);

    Task<SavedViewResponse?> UpdateAsync(
        Guid id,
        UpdateSavedViewRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}
