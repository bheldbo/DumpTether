using System.ComponentModel.DataAnnotations;

namespace DumpTether.App.Views;

public sealed record SavedViewFilterRequest(
    Guid? ProjectId = null,
    string? Status = null,
    string? Archive = null,
    string? FollowUp = null,
    int? NotViewedSinceDays = null,
    int? NotTouchedSinceDays = null,
    string? Text = null);

public sealed record SavedViewSortRequest(
    string? Field = null,
    string? Direction = null);

public sealed record CreateSavedViewRequest(
    [Required]
    [MaxLength(200)]
    string Name,
    string? Scope = null,
    SavedViewFilterRequest? Filter = null,
    SavedViewSortRequest? Sort = null,
    int SortOrder = 0);

public sealed record UpdateSavedViewRequest(
    [MaxLength(200)] string? Name = null,
    string? Scope = null,
    SavedViewFilterRequest? Filter = null,
    SavedViewSortRequest? Sort = null,
    int? SortOrder = null);

public sealed record SavedViewResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ProjectId,
    string Name,
    string Scope,
    SavedViewFilterRequest Filter,
    SavedViewSortRequest Sort,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
