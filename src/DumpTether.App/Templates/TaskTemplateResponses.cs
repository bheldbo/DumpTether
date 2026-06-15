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
    string Scope,
    bool Required,
    int SortOrder,
    IReadOnlyList<string> Options,
    int LayoutRow,
    int LayoutColumn,
    int LayoutRowSpan,
    int LayoutColumnSpan);
