namespace DumpTether.App.Templates;

public sealed record TaskTemplateSummaryResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int FieldCount);

public sealed record TaskTemplateDetailResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FieldDefinitionResponse> Fields);

public sealed record FieldDefinitionResponse(
    Guid Id,
    string Key,
    string Name,
    string Type,
    bool Required,
    int SortOrder,
    IReadOnlyList<string> Options);
