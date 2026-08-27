using DumpTether.App.Templates;
using DumpTether.Domain;

namespace DumpTether.App.Tasks;

public sealed record TaskSubtaskPreviewResponse(
    Guid Id,
    string Title,
    string? Status,
    string? Color);

public sealed record TaskItemSummaryResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ProjectId,
    Guid? TaskTemplateId,
    string Title,
    string? Status,
    string? Category,
    string? Color,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastViewedAt,
    DateTimeOffset LastTouchedAt,
    DateTimeOffset? FollowUpAt,
    DateTimeOffset? ArchivedAt,
    int NoteCount,
    IReadOnlyList<TaskItemShareResponse> Shares,
    TaskSyncStateResponse? SyncState,
    TaskTimelineEntryResponse? LatestTimelineEntry,
    Guid? ParentTaskItemId = null,
    int SubtaskCount = 0,
    string? BuiltInTemplateKind = null,
    IReadOnlyList<TaskSubtaskPreviewResponse>? SubtaskPreviews = null);

public sealed record TaskItemDetailResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ProjectId,
    Guid? TaskTemplateId,
    string Title,
    string? Status,
    string? Category,
    string? Color,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastViewedAt,
    DateTimeOffset LastTouchedAt,
    DateTimeOffset? FollowUpAt,
    DateTimeOffset? ArchivedAt,
    int NoteCount,
    IReadOnlyList<TaskItemShareResponse> Shares,
    TaskSyncStateResponse? SyncState,
    TaskTemplateDetailResponse? Template,
    IReadOnlyList<FieldValueResponse> FieldValues,
    IReadOnlyList<TaskTimelineEntryResponse> TimelineEntries,
    Guid? ParentTaskItemId = null,
    int SubtaskCount = 0,
    string? BuiltInTemplateKind = null,
    IReadOnlyList<TaskSubtaskPreviewResponse>? SubtaskPreviews = null);

public sealed record TaskItemViewCountResponse(
    Guid ViewId,
    int Count);

public sealed record TaskItemShareResponse(
    Guid Id,
    string Email,
    Guid? SharedWithUserId,
    Guid SharedByUserId,
    TaskItemShareRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt);

public sealed record TaskShareInboxResponse(
    Guid ShareId,
    Guid TaskItemId,
    Guid WorkspaceId,
    string WorkspaceName,
    string? WorkspaceColor,
    string TaskTitle,
    string SharedByEmail,
    string SharedByDisplayName,
    TaskItemShareRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? AcceptedAt);

public sealed record TaskShareLinkResponse(
    IReadOnlyList<TaskItemShareResponse> Shares,
    string Token,
    DateTimeOffset ExpiresAt);

public sealed record ShareLinkAcceptResponse(
    string Kind,
    Guid WorkspaceId,
    IReadOnlyList<Guid> TaskItemIds);

public sealed record CopyTaskItemsResponse(
    IReadOnlyList<TaskItemDetailResponse> Tasks);

public sealed record TaskItemBatchResponse(int Count);

public sealed record TaskSyncStateResponse(
    string Status,
    Guid? RemoteId,
    string? LastRemoteVersion,
    DateTimeOffset? LastAttemptedAt,
    DateTimeOffset? LastSyncedAt,
    string? LastError);

public sealed record TaskShareInboxItem(
    TaskItemShare Share,
    TaskItem TaskItem,
    Workspace Workspace,
    AppUser SharedByUser);

public sealed record FieldValueResponse(
    Guid Id,
    Guid FieldDefinitionId,
    string ValueJson,
    DateTimeOffset UpdatedAt);

public sealed record TaskTimelineEntryResponse(
    Guid Id,
    string Kind,
    string Summary,
    string? Details,
    DateTimeOffset OccurredAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FieldValueResponse> FieldValues);
