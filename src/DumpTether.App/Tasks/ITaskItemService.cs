namespace DumpTether.App.Tasks;

public interface ITaskItemService
{
    Task<TaskItemDetailResponse> CreateAsync(
        CreateTaskItemRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItemSummaryResponse>> ListAsync(
        TaskItemListScope scope,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItemSummaryResponse>> ListAsync(
        TaskItemListRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItemViewCountResponse>> CountByViewsAsync(
        IReadOnlyList<Guid> viewIds,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> UpdateAsync(
        Guid id,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken);

    Task<CopyTaskItemsResponse> CopyAsync(
        CopyTaskItemsRequest request,
        CancellationToken cancellationToken);

    Task<TaskTemplateImportResponse?> ImportTemplateAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> AddTimelineEntryAsync(
        Guid id,
        AddTaskTimelineEntryRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> UpdateTimelineEntryAsync(
        Guid taskItemId,
        Guid entryId,
        UpdateTaskTimelineEntryRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> DeleteTimelineEntryAsync(
        Guid taskItemId,
        Guid entryId,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> ArchiveAsync(
        Guid id,
        ArchiveTaskItemRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> ReopenAsync(
        Guid id,
        ReopenTaskItemRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemBatchResponse> ReopenAsync(
        ReopenTaskItemsRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemBatchResponse> DeleteArchivedAsync(
        DeleteTaskItemsRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItemShareResponse>?> ListSharesAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskShareInboxResponse>> ListIncomingSharesAsync(
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> ShareAsync(
        Guid id,
        CreateTaskShareRequest request,
        CancellationToken cancellationToken);

    Task<TaskShareLinkResponse?> CreateShareLinkAsync(
        Guid id,
        CreateTaskShareRequest request,
        CancellationToken cancellationToken);

    Task<TaskShareLinkResponse> CreateShareLinkAsync(
        CreateTaskShareLinkRequest request,
        CancellationToken cancellationToken);

    Task<ShareLinkAcceptResponse> AcceptShareLinkAsync(
        AcceptShareLinkRequest request,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> RevokeShareAsync(
        Guid id,
        Guid shareId,
        CancellationToken cancellationToken);

    Task<TaskItemDetailResponse?> UpdateShareRoleAsync(
        Guid id,
        Guid shareId,
        UpdateTaskShareRequest request,
        CancellationToken cancellationToken);

    Task<bool> LeaveShareAsync(
        Guid shareId,
        CancellationToken cancellationToken);

    Task<int> LeaveWorkspaceSharesAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);
}
