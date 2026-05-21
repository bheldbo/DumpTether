using System.ComponentModel.DataAnnotations;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Views;

internal sealed class SavedViewService : ISavedViewService
{
    private readonly IClock _clock;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly ISavedViewRepository _savedViewRepository;

    public SavedViewService(
        IClock clock,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        ISavedViewRepository savedViewRepository)
    {
        _clock = clock;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _savedViewRepository = savedViewRepository;
    }

    public async Task<IReadOnlyList<SavedViewResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var savedViews = await _savedViewRepository.ListAsync(
            context.WorkspaceId,
            cancellationToken);

        return savedViews.Select(Map).ToList();
    }

    public async Task<SavedViewResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var savedView = await _savedViewRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            trackChanges: false,
            cancellationToken);

        return savedView is null ? null : Map(savedView);
    }

    public async Task<SavedViewResponse> CreateAsync(
        CreateSavedViewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var name = NormalizeName(request.Name);
        var scope = NormalizeScope(request.Scope);
        var filter = SavedViewPayloads.NormalizeFilter(request.Filter);
        var sort = SavedViewPayloads.NormalizeSort(request.Sort);

        await EnsureUniqueActiveNameAsync(
            context.WorkspaceId,
            name,
            excludedSavedViewId: null,
            cancellationToken);

        var now = _clock.UtcNow;
        var definitionJson = SavedViewPayloads.SerializeFilter(filter);
        var sortJson = SavedViewPayloads.SerializeSort(sort);
        var savedView = scope == SavedViewScope.Project
            ? SavedView.CreateProjectView(
                context.WorkspaceId,
                RequireProjectId(filter),
                name,
                definitionJson,
                sortJson,
                request.SortOrder,
                now)
            : SavedView.CreateWorkspaceView(
                context.WorkspaceId,
                name,
                definitionJson,
                sortJson,
                request.SortOrder,
                now);

        await _savedViewRepository.AddAsync(savedView, cancellationToken);
        await _savedViewRepository.SaveChangesAsync(cancellationToken);

        return Map(savedView);
    }

    public async Task<SavedViewResponse?> UpdateAsync(
        Guid id,
        UpdateSavedViewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var savedView = await _savedViewRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            trackChanges: true,
            cancellationToken);

        if (savedView is null)
        {
            return null;
        }

        var name = request.Name is null ? savedView.Name : NormalizeName(request.Name);
        await EnsureUniqueActiveNameAsync(
            context.WorkspaceId,
            name,
            savedView.Id,
            cancellationToken);

        var scope = request.Scope is null
            ? savedView.Scope
            : NormalizeScope(request.Scope);
        var filter = request.Filter is null
            ? SavedViewPayloads.DeserializeFilter(savedView.DefinitionJson)
            : SavedViewPayloads.NormalizeFilter(request.Filter);
        var sort = request.Sort is null
            ? SavedViewPayloads.DeserializeSort(savedView.SortJson)
            : SavedViewPayloads.NormalizeSort(request.Sort);
        var sortOrder = request.SortOrder ?? savedView.SortOrder;
        var now = _clock.UtcNow;
        var definitionJson = SavedViewPayloads.SerializeFilter(filter);
        var sortJson = SavedViewPayloads.SerializeSort(sort);

        if (scope == SavedViewScope.Project)
        {
            savedView.UpdateProjectView(
                RequireProjectId(filter),
                name,
                definitionJson,
                sortJson,
                sortOrder,
                now);
        }
        else
        {
            savedView.UpdateWorkspaceView(
                name,
                definitionJson,
                sortJson,
                sortOrder,
                now);
        }

        await _savedViewRepository.SaveChangesAsync(cancellationToken);

        return Map(savedView);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var savedView = await _savedViewRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            trackChanges: true,
            cancellationToken);

        if (savedView is null)
        {
            return false;
        }

        savedView.SoftDelete(_clock.UtcNow);
        await _savedViewRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureUniqueActiveNameAsync(
        Guid workspaceId,
        string name,
        Guid? excludedSavedViewId,
        CancellationToken cancellationToken)
    {
        var exists = await _savedViewRepository.AnyActiveWithNameAsync(
            workspaceId,
            name,
            excludedSavedViewId,
            cancellationToken);

        if (exists)
        {
            throw new ValidationException($"A saved view named '{name}' already exists.");
        }
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? throw new ValidationException("Saved view name is required.")
            : name.Trim();
    }

    private static SavedViewScope NormalizeScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return SavedViewScope.Workspace;
        }

        return Enum.TryParse<SavedViewScope>(scope, ignoreCase: true, out var parsedScope) &&
            Enum.IsDefined(parsedScope)
            ? parsedScope
            : throw new ValidationException($"Unsupported saved view scope '{scope}'.");
    }

    private static Guid RequireProjectId(SavedViewFilterRequest filter)
    {
        return filter.ProjectId.HasValue && filter.ProjectId.Value != Guid.Empty
            ? filter.ProjectId.Value
            : throw new ValidationException("ProjectId is required for project-scoped views.");
    }

    private static SavedViewResponse Map(SavedView savedView)
    {
        return new SavedViewResponse(
            savedView.Id,
            savedView.WorkspaceId,
            savedView.ProjectId,
            savedView.Name,
            savedView.Scope.ToString(),
            SavedViewPayloads.DeserializeFilter(savedView.DefinitionJson),
            SavedViewPayloads.DeserializeSort(savedView.SortJson),
            savedView.SortOrder,
            savedView.CreatedAt,
            savedView.UpdatedAt);
    }
}
