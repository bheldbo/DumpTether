using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DumpTether.App.Tasks;

public sealed record CreateTaskItemRequest(
    [Required]
    [MaxLength(500)]
    string Title,
    Dictionary<Guid, JsonElement>? FieldValues = null);

public sealed record UpdateTaskItemRequest(
    [MaxLength(500)] string? Title = null,
    [MaxLength(120)] string? Status = null,
    DateTimeOffset? FollowUpAt = null,
    Dictionary<Guid, JsonElement>? FieldValues = null);
