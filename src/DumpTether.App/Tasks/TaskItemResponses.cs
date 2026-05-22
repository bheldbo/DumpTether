using DumpTether.App.Templates;

namespace DumpTether.App.Tasks;

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
    Guid? ArchiveResolutionId,
    int NoteCount,
    TaskTimelineEntryResponse? LatestTimelineEntry);

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
    Guid? ArchiveResolutionId,
    int NoteCount,
    TaskTemplateDetailResponse? Template,
    IReadOnlyList<FieldValueResponse> FieldValues,
    IReadOnlyList<TaskTimelineEntryResponse> TimelineEntries);

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
    DateTimeOffset UpdatedAt);
