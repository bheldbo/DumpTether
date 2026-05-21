namespace DumpTether.App.Tasks;

public sealed record TaskItemSummaryResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ProjectId,
    string Title,
    string? Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastViewedAt,
    DateTimeOffset LastTouchedAt,
    DateTimeOffset? FollowUpAt,
    DateTimeOffset? ArchivedAt,
    Guid? ArchiveResolutionId);

public sealed record TaskItemDetailResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ProjectId,
    string Title,
    string? Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastViewedAt,
    DateTimeOffset LastTouchedAt,
    DateTimeOffset? FollowUpAt,
    DateTimeOffset? ArchivedAt,
    Guid? ArchiveResolutionId,
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
    DateTimeOffset OccurredAt);
