using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using DumpTether.Domain;

namespace DumpTether.App.Tasks;

public sealed record TaskItemListRequest(
    Guid? ViewId = null,
    TaskItemListScope Scope = TaskItemListScope.Active,
    Guid? ProjectId = null,
    string? Status = null,
    string? Category = null,
    string? Color = null,
    string? Archive = null,
    string? FollowUp = null,
    int? NotViewedSinceDays = null,
    int? NotTouchedSinceDays = null,
    string? Text = null,
    string? SharedWith = null,
    bool SharedWithMe = false,
    string? Sort = null,
    string? Direction = null);

public sealed record CreateTaskItemRequest(
    [Required]
    [MaxLength(500)]
    string Title,
    Guid? TaskTemplateId = null,
    Dictionary<Guid, JsonElement>? FieldValues = null,
    Guid? ProjectId = null,
    [MaxLength(120)] string? Category = null);

public sealed record UpdateTaskItemRequest(
    [MaxLength(500)] string? Title = null,
    [MaxLength(120)] string? Status = null,
    [MaxLength(120)] string? Category = null,
    [MaxLength(7)] string? Color = null,
    DateTimeOffset? FollowUpAt = null,
    Dictionary<Guid, JsonElement>? FieldValues = null,
    Guid? ProjectId = null);

public sealed record AddTaskTimelineEntryRequest(
    [Required]
    [MaxLength(4000)]
    string Note);

public sealed record UpdateTaskTimelineEntryRequest(
    [Required]
    [MaxLength(4000)]
    string Note);

public sealed record ArchiveTaskItemRequest(
    [Required]
    Guid? ArchiveResolutionId,
    [MaxLength(4000)] string? Note = null);

public sealed record ReopenTaskItemRequest(
    [MaxLength(4000)] string? Note = null);

public sealed record CreateTaskShareRequest(
    [Required]
    [MaxLength(320)]
    string Email,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    TaskItemShareRole Role = TaskItemShareRole.Editor);

public sealed record CreateTaskShareLinkRequest(
    [Required]
    [MaxLength(320)]
    string Email,
    IReadOnlyList<Guid>? TaskItemIds = null,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    TaskItemShareRole Role = TaskItemShareRole.Editor);

public sealed record AcceptShareLinkRequest(
    [Required]
    string Token);

public sealed record CopyTaskItemsRequest(
    [Required]
    IReadOnlyList<Guid> TaskItemIds,
    [Required]
    Guid DestinationWorkspaceId,
    bool IncludeTimeline = false);
