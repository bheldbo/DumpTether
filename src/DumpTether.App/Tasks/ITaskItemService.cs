namespace DumpTether.App.Tasks;

public interface ITaskItemService
{
    Task<TaskItemDetailResponse> CreateAsync(
        CreateTaskItemRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItemSummaryResponse>> ListAsync(CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> UpdateAsync(
        Guid id,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken);
}
