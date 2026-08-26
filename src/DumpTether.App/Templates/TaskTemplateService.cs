using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Templates;

internal sealed class TaskTemplateService : ITaskTemplateService
{
    private const int MaxTemplateLayoutRows = 12;
    private const int MaxTemplateLayoutColumns = 6;
    private const double DefaultTemplateLayoutRowHeight = 132;
    private const double LongTextTemplateLayoutRowHeight = 190;
    private const double MinTemplateLayoutRowHeight = 72;
    private const double MaxTemplateLayoutRowHeight = 420;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly IBuiltInTaskTemplateProvisioner _builtInTaskTemplateProvisioner;
    private readonly ITaskTemplateRepository _taskTemplateRepository;

    public TaskTemplateService(
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        IBuiltInTaskTemplateProvisioner builtInTaskTemplateProvisioner,
        ITaskTemplateRepository taskTemplateRepository)
    {
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _builtInTaskTemplateProvisioner = builtInTaskTemplateProvisioner;
        _taskTemplateRepository = taskTemplateRepository;
    }

    public async Task<IReadOnlyList<TaskTemplateSummaryResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var ownerUserId = await GetTemplateOwnerUserIdAsync(
            requireWritableDevelopmentWorkspace: false,
            cancellationToken);
        await EnsureBuiltInTemplatesAsync(ownerUserId, cancellationToken);
        var templates = await _taskTemplateRepository.ListAsync(
            ownerUserId,
            cancellationToken);

        return templates
            .Select(MapSummary)
            .ToList();
    }

    public async Task<TaskTemplateDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await GetTemplateOwnerUserIdAsync(
            requireWritableDevelopmentWorkspace: false,
            cancellationToken);
        await EnsureBuiltInTemplatesAsync(ownerUserId, cancellationToken);
        var template = await _taskTemplateRepository.GetByIdAsync(
            id,
            ownerUserId,
            trackChanges: false,
            includeDeleted: false,
            cancellationToken);

        return template is null ? null : MapDetail(template);
    }

    public async Task<TaskTemplateDetailResponse> CreateAsync(
        CreateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownerUserId = await GetTemplateOwnerUserIdAsync(
            requireWritableDevelopmentWorkspace: true,
            cancellationToken);
        await EnsureBuiltInTemplatesAsync(ownerUserId, cancellationToken);
        var name = NormalizeName(request.Name);
        await EnsureUniqueActiveNameAsync(
            ownerUserId,
            name,
            excludedTemplateId: null,
            cancellationToken);

        var now = _clock.UtcNow;
        var template = TaskTemplate.Create(ownerUserId, name, now);
        var normalizedFields = NormalizeFields(request.Fields);
        var normalizedLayout = NormalizeLayout(request.Layout, normalizedFields);
        template.UpdateLayout(
            SerializeLayoutRows(normalizedLayout.Header),
            SerializeLayoutRows(normalizedLayout.Entry),
            now);

        foreach (var field in normalizedFields)
        {
            template.AddFieldDefinition(
                field.Key,
                field.Name,
                field.Type,
                field.Scope,
                field.Required,
                field.SortOrder,
                field.OptionsJson,
                field.LayoutRow,
                field.LayoutColumn,
                field.LayoutRowSpan,
                field.LayoutColumnSpan,
                field.LayoutWeight);
        }

        await _taskTemplateRepository.AddAsync(template, cancellationToken);
        await _taskTemplateRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(template);
    }

    public async Task<TaskTemplateDetailResponse?> UpdateAsync(
        Guid id,
        UpdateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownerUserId = await GetTemplateOwnerUserIdAsync(
            requireWritableDevelopmentWorkspace: true,
            cancellationToken);
        var template = await _taskTemplateRepository.GetByIdAsync(
            id,
            ownerUserId,
            trackChanges: true,
            includeDeleted: false,
            cancellationToken);

        if (template is null)
        {
            return null;
        }

        if (template.IsProtected)
        {
            throw new ValidationException("Built-in task templates cannot be changed.");
        }

        var now = _clock.UtcNow;

        if (request.Name is not null)
        {
            var name = NormalizeName(request.Name);
            await EnsureUniqueActiveNameAsync(
                ownerUserId,
                name,
                template.Id,
                cancellationToken);

            template.Rename(name, now);
        }

        if (request.Fields is not null || request.Layout is not null)
        {
            var normalizedFields = request.Fields is null
                ? template.FieldDefinitions
                    .Where(field => field.IsActive)
                    .OrderBy(field => field.Scope)
                    .ThenBy(field => field.SortOrder)
                    .ThenBy(field => field.Label)
                    .Select(MapExistingFieldToNormalizedField)
                    .ToList()
                : NormalizeFields(request.Fields);
            var normalizedLayout = NormalizeLayout(request.Layout, normalizedFields);

            if (request.Fields is not null)
            {
                ApplyFieldDefinitions(template, normalizedFields, now);
            }

            template.UpdateLayout(
                SerializeLayoutRows(normalizedLayout.Header),
                SerializeLayoutRows(normalizedLayout.Entry),
                now);
        }

        await _taskTemplateRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(template);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ownerUserId = await GetTemplateOwnerUserIdAsync(
            requireWritableDevelopmentWorkspace: true,
            cancellationToken);
        var template = await _taskTemplateRepository.GetByIdAsync(
            id,
            ownerUserId,
            trackChanges: true,
            includeDeleted: false,
            cancellationToken);

        if (template is null)
        {
            return false;
        }

        if (template.IsProtected)
        {
            throw new ValidationException("Built-in task templates cannot be deleted.");
        }

        template.SoftDelete(_clock.UtcNow);
        await _taskTemplateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<Guid?> GetTemplateOwnerUserIdAsync(
        bool requireWritableDevelopmentWorkspace,
        CancellationToken cancellationToken)
    {
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (currentSession is not null)
        {
            return currentSession.UserId;
        }

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);

        if (requireWritableDevelopmentWorkspace)
        {
            EnsureCanWriteDevelopmentWorkspace(context);
        }

        return null;
    }

    private async Task EnsureBuiltInTemplatesAsync(
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        await _builtInTaskTemplateProvisioner.EnsureAsync(ownerUserId, cancellationToken);
        await _taskTemplateRepository.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureCanWriteDevelopmentWorkspace(DevelopmentWorkspaceContext context)
    {
        if (!context.CanWriteWorkspace)
        {
            throw new ValidationException("Read-only board access cannot change the development template library.");
        }
    }

    private static void ApplyFieldDefinitions(
        TaskTemplate template,
        IReadOnlyList<NormalizedFieldDefinition> requestedFields,
        DateTimeOffset updatedAt)
    {
        var activeFields = template.FieldDefinitions
            .Where(field => field.IsActive)
            .ToDictionary(field => field.Id);
        var requestedExistingIds = new HashSet<Guid>();

        foreach (var requestedField in requestedFields)
        {
            if (requestedField.Id.HasValue)
            {
                if (!activeFields.TryGetValue(requestedField.Id.Value, out var existingField))
                {
                    throw new ValidationException(
                        $"Field definition '{requestedField.Id.Value}' was not found on this template.");
                }

                existingField.Update(
                    requestedField.Key,
                    requestedField.Name,
                    requestedField.Type,
                    requestedField.Scope,
                    requestedField.Required,
                    requestedField.SortOrder,
                    requestedField.OptionsJson,
                    requestedField.LayoutRow,
                    requestedField.LayoutColumn,
                    requestedField.LayoutRowSpan,
                    requestedField.LayoutColumnSpan,
                    requestedField.LayoutWeight);

                requestedExistingIds.Add(existingField.Id);
                continue;
            }

            template.AddFieldDefinition(
                requestedField.Key,
                requestedField.Name,
                requestedField.Type,
                requestedField.Scope,
                requestedField.Required,
                requestedField.SortOrder,
                requestedField.OptionsJson,
                requestedField.LayoutRow,
                requestedField.LayoutColumn,
                requestedField.LayoutRowSpan,
                requestedField.LayoutColumnSpan,
                requestedField.LayoutWeight);
        }

        foreach (var activeField in activeFields.Values)
        {
            if (!requestedExistingIds.Contains(activeField.Id))
            {
                activeField.Deactivate(updatedAt);
            }
        }
    }

    private async Task EnsureUniqueActiveNameAsync(
        Guid? ownerUserId,
        string name,
        Guid? excludedTemplateId,
        CancellationToken cancellationToken)
    {
        var exists = await _taskTemplateRepository.AnyActiveWithNameAsync(
            ownerUserId,
            name,
            excludedTemplateId,
            cancellationToken);

        if (exists)
        {
            throw new ValidationException($"A template named '{name}' already exists.");
        }
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? throw new ValidationException("Template name is required.")
            : name.Trim();
    }

    private static IReadOnlyList<NormalizedFieldDefinition> NormalizeFields(
        IReadOnlyList<UpsertFieldDefinitionRequest>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return [];
        }

        var normalizedFields = fields
            .Select((field, index) => NormalizeField(field, index))
            .ToList();
        var duplicateKey = normalizedFields
            .GroupBy(field => new FieldKey(field.Scope, field.Key.ToLowerInvariant()))
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key.Key;

        if (duplicateKey is not null)
        {
            throw new ValidationException(
                $"Template fields must have unique names. Duplicate key: '{duplicateKey}'.");
        }

        return normalizedFields;
    }

    private static NormalizedTemplateLayout NormalizeLayout(
        TaskTemplateLayoutRequest? layout,
        IReadOnlyList<NormalizedFieldDefinition> fields)
    {
        return new NormalizedTemplateLayout(
            NormalizeLayoutRows(layout?.Header, FieldDefinitionScope.Header, fields),
            NormalizeLayoutRows(layout?.Entry, FieldDefinitionScope.Entry, fields));
    }

    private static IReadOnlyList<TaskTemplateLayoutRowResponse> NormalizeLayoutRows(
        IReadOnlyList<TaskTemplateLayoutRowRequest>? requestedRows,
        FieldDefinitionScope scope,
        IEnumerable<NormalizedFieldDefinition> fields)
    {
        var scopedFields = fields
            .Where(field => field.Scope == scope)
            .ToList();

        if (scopedFields.Count == 0)
        {
            return [];
        }

        var rowsByNumber = (requestedRows ?? [])
            .Where(row => row.Row >= 1 && row.Row <= MaxTemplateLayoutRows)
            .GroupBy(row => row.Row)
            .ToDictionary(group => group.Key, group => group.Last());
        var rowCount = Math.Max(
            rowsByNumber.Keys.DefaultIfEmpty(1).Max(),
            scopedFields.Select(field => field.LayoutRow).DefaultIfEmpty(1).Max());
        rowCount = Math.Min(MaxTemplateLayoutRows, rowCount);

        return Enumerable.Range(1, rowCount)
            .Select(rowNumber =>
            {
                rowsByNumber.TryGetValue(rowNumber, out var requestedRow);
                var rowFields = scopedFields
                    .Where(field => field.LayoutRow == rowNumber)
                    .ToList();
                var columnCount = Math.Max(
                    1,
                    Math.Max(
                        requestedRow?.ColumnWeights?.Count ?? 0,
                        rowFields.Select(field => field.LayoutColumn).DefaultIfEmpty(1).Max()));
                columnCount = Math.Min(MaxTemplateLayoutColumns, columnCount);
                var weights = NormalizeColumnWeights(
                    requestedRow?.ColumnWeights,
                    columnCount,
                    rowFields);
                var height = NormalizeLayoutHeight(
                    requestedRow?.Height,
                    rowFields.Any(field => field.Type == FieldDefinitionType.LongText)
                        ? LongTextTemplateLayoutRowHeight
                        : DefaultTemplateLayoutRowHeight);

                return new TaskTemplateLayoutRowResponse(rowNumber, weights, height);
            })
            .ToList();
    }

    private static IReadOnlyList<TaskTemplateLayoutRowResponse> NormalizeExistingLayoutRows(
        IReadOnlyList<TaskTemplateLayoutRowResponse> storedRows,
        FieldDefinitionScope scope,
        IEnumerable<FieldDefinition> fields)
    {
        var scopedFields = fields
            .Where(field => field.Scope == scope)
            .Select(MapExistingFieldToNormalizedField)
            .ToList();

        return NormalizeLayoutRows(
            storedRows
                .Select(row => new TaskTemplateLayoutRowRequest(
                    row.Row,
                    row.ColumnWeights,
                    row.Height))
                .ToList(),
            scope,
            scopedFields);
    }

    private static IReadOnlyList<double> NormalizeColumnWeights(
        IReadOnlyList<double>? requestedWeights,
        int columnCount,
        IEnumerable<NormalizedFieldDefinition> rowFields)
    {
        var weights = requestedWeights is null || requestedWeights.Count == 0
            ? new List<double>()
            : requestedWeights
                .Take(MaxTemplateLayoutColumns)
                .Select(weight => NormalizeLayoutWeight(weight))
                .ToList();
        var fieldsByColumn = rowFields
            .GroupBy(field => field.LayoutColumn)
            .ToDictionary(group => group.Key, group => group.First());

        return Enumerable.Range(1, columnCount)
            .Select(column =>
            {
                if (column <= weights.Count)
                {
                    return weights[column - 1];
                }

                return fieldsByColumn.TryGetValue(column, out var field)
                    ? field.LayoutWeight
                    : 1;
            })
            .Select(weight => Math.Round(weight, 4))
            .ToList();
    }

    private static double NormalizeLayoutHeight(double? value, double defaultValue)
    {
        var normalizedValue = value ?? defaultValue;

        if (double.IsNaN(normalizedValue) || double.IsInfinity(normalizedValue))
        {
            return defaultValue;
        }

        return Math.Round(
            Math.Min(MaxTemplateLayoutRowHeight, Math.Max(MinTemplateLayoutRowHeight, normalizedValue)),
            2);
    }

    private static string SerializeLayoutRows(IReadOnlyList<TaskTemplateLayoutRowResponse> rows)
    {
        return JsonSerializer.Serialize(rows, JsonSerializerOptions);
    }

    private static IReadOnlyList<TaskTemplateLayoutRowResponse> ParseLayoutRows(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<TaskTemplateLayoutRowResponse>>(
                    layoutJson,
                    JsonSerializerOptions) ??
                [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static NormalizedFieldDefinition MapExistingFieldToNormalizedField(
        FieldDefinition field)
    {
        return new NormalizedFieldDefinition(
            field.Id,
            field.Key,
            field.Label,
            field.Type,
            field.Scope,
            field.IsRequired,
            field.SortOrder,
            field.OptionsJson,
            field.LayoutRow,
            field.LayoutColumn,
            field.LayoutRowSpan,
            field.LayoutColumnSpan,
            field.LayoutWeight);
    }

    private static NormalizedFieldDefinition NormalizeField(
        UpsertFieldDefinitionRequest request,
        int fallbackSortOrder)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Field name is required.");
        }

        if (!Enum.TryParse<FieldDefinitionType>(
                request.Type,
                ignoreCase: true,
                out var type) ||
            !Enum.IsDefined(type))
        {
            throw new ValidationException($"Unsupported field type '{request.Type}'.");
        }

        var scope = NormalizeScope(request.Scope);
        var name = request.Name.Trim();
        var options = NormalizeOptions(type, request.Options);
        var layoutRow = NormalizeLayoutValue(
            request.LayoutRow,
            defaultValue: 1,
            maxValue: 12,
            label: "layout row");
        var layoutColumn = NormalizeLayoutValue(
            request.LayoutColumn,
            defaultValue: 1,
            maxValue: 12,
            label: "layout column");
        var layoutRowSpan = NormalizeLayoutValue(
            request.LayoutRowSpan,
            defaultValue: 1,
            maxValue: 6,
            label: "layout row span");
        var layoutColumnSpan = NormalizeLayoutValue(
            request.LayoutColumnSpan,
            defaultValue: 1,
            maxValue: 12,
            label: "layout column span");
        var layoutWeight = NormalizeLayoutWeight(request.LayoutWeight);

        return new NormalizedFieldDefinition(
            request.Id == Guid.Empty ? null : request.Id,
            GenerateKey(name),
            name,
            type,
            scope,
            request.Required,
            request.SortOrder >= 0 ? request.SortOrder : fallbackSortOrder,
            options.Count == 0
                ? null
                : JsonSerializer.Serialize(options, JsonSerializerOptions),
            layoutRow,
            layoutColumn,
            layoutRowSpan,
            layoutColumnSpan,
            layoutWeight);
    }

    private static IReadOnlyList<string> NormalizeOptions(
        FieldDefinitionType type,
        IReadOnlyList<string>? options)
    {
        if (type != FieldDefinitionType.Select)
        {
            return [];
        }

        var normalizedOptions = (options ?? [])
            .Select(option => option.Trim())
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedOptions.Count == 0)
        {
            throw new ValidationException("Select fields require at least one option.");
        }

        return normalizedOptions;
    }

    private static string GenerateKey(string name)
    {
        var keyCharacters = name
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        var key = string.Join(
            '_',
            new string(keyCharacters)
                .Split('_', StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(key) ? "field" : key;
    }

    internal static TaskTemplateSummaryResponse MapSummary(TaskTemplate template)
    {
        return new TaskTemplateSummaryResponse(
            template.Id,
            template.Name,
            template.CreatedAt,
            template.UpdatedAt,
            template.FieldDefinitions.Count(field => field.IsActive),
            MapBuiltInKind(template),
            template.IsProtected);
    }

    internal static TaskTemplateDetailResponse MapDetail(TaskTemplate template)
    {
        var activeFields = template.FieldDefinitions
            .Where(field => field.IsActive)
            .OrderBy(field => field.Scope)
            .ThenBy(field => field.SortOrder)
            .ThenBy(field => field.Label)
            .ToList();

        return new TaskTemplateDetailResponse(
            template.Id,
            template.Name,
            template.CreatedAt,
            template.UpdatedAt,
            MapLayout(template, activeFields),
            activeFields
                .Select(MapField)
                .ToList(),
            MapBuiltInKind(template),
            template.IsProtected);
    }

    private static string? MapBuiltInKind(TaskTemplate template) =>
        template.BuiltInKind == TaskTemplateBuiltInKind.None
            ? null
            : template.BuiltInKind.ToString();

    internal static TaskTemplateLayoutResponse MapLayout(
        TaskTemplate template,
        IEnumerable<FieldDefinition> fields)
    {
        var fieldList = fields.ToList();

        return new TaskTemplateLayoutResponse(
            NormalizeExistingLayoutRows(
                ParseLayoutRows(template.HeaderLayoutJson),
                FieldDefinitionScope.Header,
                fieldList),
            NormalizeExistingLayoutRows(
                ParseLayoutRows(template.EntryLayoutJson),
                FieldDefinitionScope.Entry,
                fieldList));
    }

    internal static FieldDefinitionResponse MapField(FieldDefinition fieldDefinition)
    {
        return new FieldDefinitionResponse(
            fieldDefinition.Id,
            fieldDefinition.Key,
            fieldDefinition.Label,
            fieldDefinition.Type.ToString(),
            fieldDefinition.Scope.ToString(),
            fieldDefinition.IsRequired,
            fieldDefinition.SortOrder,
            ParseOptions(fieldDefinition.OptionsJson),
            fieldDefinition.LayoutRow,
            fieldDefinition.LayoutColumn,
            fieldDefinition.LayoutRowSpan,
            fieldDefinition.LayoutColumnSpan,
            fieldDefinition.LayoutWeight);
    }

    internal static IReadOnlyList<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(
                    optionsJson,
                    JsonSerializerOptions) ??
                [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record NormalizedFieldDefinition(
        Guid? Id,
        string Key,
        string Name,
        FieldDefinitionType Type,
        FieldDefinitionScope Scope,
        bool Required,
        int SortOrder,
        string? OptionsJson,
        int LayoutRow,
        int LayoutColumn,
        int LayoutRowSpan,
        int LayoutColumnSpan,
        double LayoutWeight);

    private sealed record NormalizedTemplateLayout(
        IReadOnlyList<TaskTemplateLayoutRowResponse> Header,
        IReadOnlyList<TaskTemplateLayoutRowResponse> Entry);

    private sealed record FieldKey(FieldDefinitionScope Scope, string Key);

    private static FieldDefinitionScope NormalizeScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return FieldDefinitionScope.Header;
        }

        if (!Enum.TryParse<FieldDefinitionScope>(
                scope,
                ignoreCase: true,
                out var parsedScope) ||
            !Enum.IsDefined(parsedScope))
        {
            throw new ValidationException($"Unsupported field scope '{scope}'.");
        }

        return parsedScope;
    }

    private static int NormalizeLayoutValue(
        int? value,
        int defaultValue,
        int maxValue,
        string label)
    {
        var normalizedValue = value ?? defaultValue;

        if (normalizedValue < 1 || normalizedValue > maxValue)
        {
            throw new ValidationException($"Field {label} must be between 1 and {maxValue}.");
        }

        return normalizedValue;
    }

    private static double NormalizeLayoutWeight(double? value)
    {
        var normalizedValue = value ?? 1;

        if (double.IsNaN(normalizedValue) ||
            double.IsInfinity(normalizedValue) ||
            normalizedValue is < 0.1 or > 12)
        {
            throw new ValidationException("Field layout weight must be between 0.1 and 12.");
        }

        return Math.Round(normalizedValue, 4);
    }
}
