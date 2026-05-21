namespace DumpTether.App.ArchiveResolutions;

public sealed record ArchiveResolutionResponse(
    Guid Id,
    string Name,
    string? Description,
    bool RequiresExplanation);
