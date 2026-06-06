using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Templates;

internal sealed class TaskTemplateService : ITaskTemplateService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IClock _clock;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly ITaskTemplateRepository _taskTemplateRepository;

    public TaskTemplateService(
        IClock clock,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        ITaskTemplateRepository taskTemplateRepository)
    {
        _clock = clock;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _taskTemplateRepository = taskTemplateRepository;
    }

    public async Task<IReadOnlyList<TaskTemplateSummaryResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var templates = await _taskTemplateRepository.ListAsync(
            context.WorkspaceId,
            cancellationToken);

        return templates
            .Select(MapSummary)
            .ToList();
    }

    public async Task<TaskTemplateDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var template = await _taskTemplateRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
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

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanWriteWorkspace(context);
        var name = NormalizeName(request.Name);
        await EnsureUniqueActiveNameAsync(
            context.WorkspaceId,
            name,
            excludedTemplateId: null,
            cancellationToken);

        var now = _clock.UtcNow;
        var template = TaskTemplate.Create(context.WorkspaceId, name, now);

        foreach (var field in NormalizeFields(request.Fields))
        {
            template.AddFieldDefinition(
                field.Key,
                field.Name,
                field.Type,
                field.Required,
                field.SortOrder,
                field.OptionsJson);
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

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanWriteWorkspace(context);
        var template = await _taskTemplateRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            trackChanges: true,
            includeDeleted: false,
            cancellationToken);

        if (template is null)
        {
            return null;
        }

        var now = _clock.UtcNow;

        if (request.Name is not null)
        {
            var name = NormalizeName(request.Name);
            await EnsureUniqueActiveNameAsync(
                context.WorkspaceId,
                name,
                template.Id,
                cancellationToken);

            template.Rename(name, now);
        }

        if (request.Fields is not null)
        {
            ApplyFieldDefinitions(template, NormalizeFields(request.Fields), now);
            template.MarkUpdated(now);
        }

        await _taskTemplateRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(template);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanWriteWorkspace(context);
        var template = await _taskTemplateRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            trackChanges: true,
            includeDeleted: false,
            cancellationToken);

        if (template is null)
        {
            return false;
        }

        template.SoftDelete(_clock.UtcNow);
        await _taskTemplateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void EnsureCanWriteWorkspace(DevelopmentWorkspaceContext context)
    {
        if (!context.CanWriteWorkspace)
        {
            throw new ValidationException("Read-only board access cannot change templates.");
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
                    requestedField.Required,
                    requestedField.SortOrder,
                    requestedField.OptionsJson);

                requestedExistingIds.Add(existingField.Id);
                continue;
            }

            template.AddFieldDefinition(
                requestedField.Key,
                requestedField.Name,
                requestedField.Type,
                requestedField.Required,
                requestedField.SortOrder,
                requestedField.OptionsJson);
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
        Guid workspaceId,
        string name,
        Guid? excludedTemplateId,
        CancellationToken cancellationToken)
    {
        var exists = await _taskTemplateRepository.AnyActiveWithNameAsync(
            workspaceId,
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
            .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateKey is not null)
        {
            throw new ValidationException(
                $"Template fields must have unique names. Duplicate key: '{duplicateKey}'.");
        }

        return normalizedFields;
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

        var name = request.Name.Trim();
        var options = NormalizeOptions(type, request.Options);

        return new NormalizedFieldDefinition(
            request.Id == Guid.Empty ? null : request.Id,
            GenerateKey(name),
            name,
            type,
            request.Required,
            request.SortOrder >= 0 ? request.SortOrder : fallbackSortOrder,
            options.Count == 0
                ? null
                : JsonSerializer.Serialize(options, JsonSerializerOptions));
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
            template.FieldDefinitions.Count(field => field.IsActive));
    }

    internal static TaskTemplateDetailResponse MapDetail(TaskTemplate template)
    {
        return new TaskTemplateDetailResponse(
            template.Id,
            template.Name,
            template.CreatedAt,
            template.UpdatedAt,
            template.FieldDefinitions
                .Where(field => field.IsActive)
                .OrderBy(field => field.SortOrder)
                .ThenBy(field => field.Label)
                .Select(MapField)
                .ToList());
    }

    internal static FieldDefinitionResponse MapField(FieldDefinition fieldDefinition)
    {
        return new FieldDefinitionResponse(
            fieldDefinition.Id,
            fieldDefinition.Key,
            fieldDefinition.Label,
            fieldDefinition.Type.ToString(),
            fieldDefinition.IsRequired,
            fieldDefinition.SortOrder,
            ParseOptions(fieldDefinition.OptionsJson));
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
        bool Required,
        int SortOrder,
        string? OptionsJson);
}
