namespace DumpTether.App.Tasks;

public enum TaskItemArchiveFilter
{
    Active = 1,
    Archived = 2,
    All = 3
}

public enum TaskItemFollowUpFilter
{
    None = 0,
    Any = 1,
    Overdue = 2,
    Today = 3,
    ThisWeek = 4
}

public enum TaskItemSortField
{
    LastTouchedAt = 1,
    CreatedAt = 2,
    FollowUpAt = 3,
    Title = 4,
    Status = 5
}

public sealed record TaskItemQuery(
    Guid WorkspaceId,
    Guid? ProjectId,
    string? Status,
    string? Category,
    string? Color,
    TaskItemArchiveFilter ArchiveFilter,
    TaskItemFollowUpFilter FollowUpFilter,
    int? NotViewedSinceDays,
    int? NotTouchedSinceDays,
    string? Text,
    string? SharedWith,
    Guid? SharedAccessUserId,
    string? SharedAccessNormalizedEmail,
    bool LimitToSharedAccess,
    bool SharedWithMe,
    TaskItemSortField SortField,
    bool SortDescending,
    DateTimeOffset Now);
