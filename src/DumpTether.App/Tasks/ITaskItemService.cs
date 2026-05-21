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

    Task<TaskItemDetailResponse?> AddTimelineEntryAsync(
        Guid id,
        AddTaskTimelineEntryRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> ArchiveAsync(
        Guid id,
        ArchiveTaskItemRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> ReopenAsync(
        Guid id,
        ReopenTaskItemRequest request,
        CancellationToken cancellationToken);
}
