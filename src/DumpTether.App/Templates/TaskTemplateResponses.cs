namespace DumpTether.App.Templates;

public sealed record TaskTemplateSummaryResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int FieldCount,
    string? BuiltInKind,
    bool IsProtected);

public sealed record TaskTemplateDetailResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    TaskTemplateLayoutResponse Layout,
    IReadOnlyList<FieldDefinitionResponse> Fields,
    string? BuiltInKind,
    bool IsProtected);

public sealed record TaskTemplateLayoutResponse(
    IReadOnlyList<TaskTemplateLayoutRowResponse> Header,
    IReadOnlyList<TaskTemplateLayoutRowResponse> Entry);

public sealed record TaskTemplateLayoutRowResponse(
    int Row,
    IReadOnlyList<double> ColumnWeights,
    double Height);

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
    int LayoutColumnSpan,
    double LayoutWeight);
