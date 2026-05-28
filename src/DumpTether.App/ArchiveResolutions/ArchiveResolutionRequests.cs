using System.ComponentModel.DataAnnotations;

namespace DumpTether.App.ArchiveResolutions;

public sealed record CreateArchiveResolutionRequest(
    [Required]
    [MaxLength(120)]
    string Name,
    [MaxLength(500)]
    string? Description = null,
    bool RequiresExplanation = false);

public sealed record UpdateArchiveResolutionRequest(
    [MaxLength(120)]
    string? Name = null,
    [MaxLength(500)]
    string? Description = null,
    bool? RequiresExplanation = null);
