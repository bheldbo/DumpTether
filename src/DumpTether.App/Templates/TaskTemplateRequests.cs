using System.ComponentModel.DataAnnotations;

namespace DumpTether.App.Templates;

public sealed record CreateTaskTemplateRequest(
    [Required]
    [MaxLength(200)]
    string Name,
    IReadOnlyList<UpsertFieldDefinitionRequest>? Fields = null,
    TaskTemplateLayoutRequest? Layout = null);

public sealed record UpdateTaskTemplateRequest(
    [MaxLength(200)]
    string? Name = null,
    IReadOnlyList<UpsertFieldDefinitionRequest>? Fields = null,
    TaskTemplateLayoutRequest? Layout = null);

public sealed record TaskTemplateLayoutRequest(
    IReadOnlyList<TaskTemplateLayoutRowRequest>? Header = null,
    IReadOnlyList<TaskTemplateLayoutRowRequest>? Entry = null);

public sealed record TaskTemplateLayoutRowRequest(
    int Row,
    IReadOnlyList<double>? ColumnWeights = null,
    double? Height = null);

public sealed record UpsertFieldDefinitionRequest(
    Guid? Id,
    [Required]
    [MaxLength(200)]
    string Name,
    [Required]
    string Type,
    string? Scope,
    bool Required,
    int SortOrder,
    IReadOnlyList<string>? Options = null,
    int? LayoutRow = null,
    int? LayoutColumn = null,
    int? LayoutRowSpan = null,
    int? LayoutColumnSpan = null,
    double? LayoutWeight = null);
