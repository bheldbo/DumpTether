using DumpTether.App.Tasks;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfTaskItemRepository : ITaskItemRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfTaskItemRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        await _dbContext.TaskItems.AddAsync(taskItem, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        TaskItemListScope scope,
        CancellationToken cancellationToken)
    {
        return await ListAsync(
            new TaskItemQuery(
                workspaceId,
                projectId,
                Status: null,
                Category: null,
                Color: null,
                MapScope(scope),
                TaskItemFollowUpFilter.None,
                NotViewedSinceDays: null,
                NotTouchedSinceDays: null,
                Text: null,
                TaskItemSortField.LastTouchedAt,
                SortDescending: true,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> ListAsync(
        TaskItemQuery query,
        CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.TaskItems
            .AsNoTracking()
            .Include("_fieldValues")
            .Include("_timelineEntries")
            .AsSplitQuery()
            .Where(taskItem => taskItem.WorkspaceId == query.WorkspaceId)
            .ToListAsync(cancellationToken);

        return SortTaskItems(candidates.Where(taskItem => MatchesQuery(taskItem, query)), query)
            .ThenBy(taskItem => taskItem.Title)
            .ToList();
    }

    public async Task<IReadOnlyList<TaskItem>> ListByProjectAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.TaskItems
            .Include("_timelineEntries")
            .AsSplitQuery()
            .Where(taskItem =>
                taskItem.WorkspaceId == workspaceId &&
                taskItem.ProjectId == projectId);

        if (!includeArchived)
        {
            query = query.Where(taskItem => taskItem.ArchivedAt == null);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        Guid workspaceId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.TaskItems
            .AsNoTracking()
            .Where(taskItem => taskItem.WorkspaceId == workspaceId);

        if (!includeArchived)
        {
            query = query.Where(taskItem => taskItem.ArchivedAt == null);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<TaskItem?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        Guid? projectId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.TaskItems
            .Include("_fieldValues")
            .Include("_timelineEntries")
            .AsSplitQuery()
            .Where(taskItem =>
                taskItem.Id == id &&
                taskItem.WorkspaceId == workspaceId &&
                (!projectId.HasValue || taskItem.ProjectId == projectId.Value));

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, FieldDefinition>> GetFieldDefinitionsAsync(
        IEnumerable<Guid> fieldDefinitionIds,
        CancellationToken cancellationToken)
    {
        var ids = fieldDefinitionIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, FieldDefinition>();
        }

        return await _dbContext.FieldDefinitions
            .Where(fieldDefinition => ids.Contains(fieldDefinition.Id))
            .ToDictionaryAsync(
                fieldDefinition => fieldDefinition.Id,
                cancellationToken);
    }

    public async Task<TaskTemplate?> GetTaskTemplateByIdAsync(
        Guid id,
        Guid workspaceId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.TaskTemplates
            .Include("_fieldDefinitions")
            .AsSplitQuery()
            .AsNoTracking()
            .Where(template =>
                template.Id == id &&
                template.WorkspaceId == workspaceId);

        if (!includeDeleted)
        {
            query = query.Where(template => template.DeletedAt == null);
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TaskTemplate?> GetDefaultTaskTemplateAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.TaskTemplates
            .Include("_fieldDefinitions")
            .AsSplitQuery()
            .AsNoTracking()
            .Where(template =>
                template.WorkspaceId == workspaceId &&
                template.DeletedAt == null)
            .OrderByDescending(template => template.Name == "Basic Task")
            .ThenBy(template => template.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ArchiveResolution?> GetArchiveResolutionByIdAsync(
        Guid id,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ArchiveResolutions
            .SingleOrDefaultAsync(
                archiveResolution =>
                    archiveResolution.Id == id &&
                    archiveResolution.WorkspaceId == workspaceId &&
                    archiveResolution.IsActive,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TaskItemArchiveFilter MapScope(TaskItemListScope scope)
    {
        return scope switch
        {
            TaskItemListScope.Archive => TaskItemArchiveFilter.Archived,
            TaskItemListScope.All => TaskItemArchiveFilter.All,
            _ => TaskItemArchiveFilter.Active
        };
    }

    private static bool MatchesQuery(TaskItem taskItem, TaskItemQuery query)
    {
        return MatchesProject(taskItem, query.ProjectId) &&
            MatchesArchive(taskItem, query.ArchiveFilter) &&
            MatchesStatus(taskItem, query.Status) &&
            MatchesCategory(taskItem, query.Category) &&
            MatchesColor(taskItem, query.Color) &&
            MatchesFollowUp(taskItem, query.FollowUpFilter, query.Now) &&
            MatchesNotViewedSince(taskItem, query.NotViewedSinceDays, query.Now) &&
            MatchesNotTouchedSince(taskItem, query.NotTouchedSinceDays, query.Now) &&
            MatchesText(taskItem, query.Text);
    }

    private static bool MatchesProject(TaskItem taskItem, Guid? projectId)
    {
        return !projectId.HasValue || taskItem.ProjectId == projectId.Value;
    }

    private static bool MatchesArchive(TaskItem taskItem, TaskItemArchiveFilter archiveFilter)
    {
        return archiveFilter switch
        {
            TaskItemArchiveFilter.Archived => taskItem.ArchivedAt.HasValue,
            TaskItemArchiveFilter.All => true,
            _ => !taskItem.ArchivedAt.HasValue
        };
    }

    private static bool MatchesStatus(TaskItem taskItem, string? status)
    {
        if (status is null)
        {
            return true;
        }

        if (status.Length == 0)
        {
            return string.IsNullOrWhiteSpace(taskItem.Status);
        }

        return string.Equals(taskItem.Status, status, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCategory(TaskItem taskItem, string? category)
    {
        if (category is null)
        {
            return true;
        }

        if (category.Length == 0)
        {
            return string.IsNullOrWhiteSpace(taskItem.Category);
        }

        return string.Equals(taskItem.Category, category, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesColor(TaskItem taskItem, string? color)
    {
        if (color is null)
        {
            return true;
        }

        if (color.Length == 0)
        {
            return string.IsNullOrWhiteSpace(taskItem.Color);
        }

        return string.Equals(taskItem.Color, color, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesFollowUp(
        TaskItem taskItem,
        TaskItemFollowUpFilter followUpFilter,
        DateTimeOffset now)
    {
        var today = new DateTimeOffset(now.Date, now.Offset);

        return followUpFilter switch
        {
            TaskItemFollowUpFilter.Any => taskItem.FollowUpAt.HasValue,
            TaskItemFollowUpFilter.Overdue => taskItem.FollowUpAt.HasValue &&
                taskItem.FollowUpAt.Value < now,
            TaskItemFollowUpFilter.Today => taskItem.FollowUpAt.HasValue &&
                taskItem.FollowUpAt.Value.Date == now.Date,
            TaskItemFollowUpFilter.ThisWeek => taskItem.FollowUpAt.HasValue &&
                taskItem.FollowUpAt.Value >= today &&
                taskItem.FollowUpAt.Value < today.AddDays(7),
            _ => true
        };
    }

    private static bool MatchesNotViewedSince(
        TaskItem taskItem,
        int? notViewedSinceDays,
        DateTimeOffset now)
    {
        if (!notViewedSinceDays.HasValue)
        {
            return true;
        }

        var threshold = now.AddDays(-notViewedSinceDays.Value);
        return !taskItem.LastViewedAt.HasValue || taskItem.LastViewedAt.Value <= threshold;
    }

    private static bool MatchesNotTouchedSince(
        TaskItem taskItem,
        int? notTouchedSinceDays,
        DateTimeOffset now)
    {
        if (!notTouchedSinceDays.HasValue)
        {
            return true;
        }

        return taskItem.LastTouchedAt <= now.AddDays(-notTouchedSinceDays.Value);
    }

    private static bool MatchesText(TaskItem taskItem, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return ContainsText(taskItem.Title, text) ||
            ContainsText(taskItem.Status, text) ||
            ContainsText(taskItem.Category, text) ||
            ContainsText(taskItem.Color, text) ||
            taskItem.FieldValues.Any(value => ContainsText(value.ValueJson, text)) ||
            taskItem.TimelineEntries.Any(entry =>
                entry.DeletedAt == null &&
                (ContainsText(entry.Summary, text) ||
                 ContainsText(entry.Details, text)));
    }

    private static bool ContainsText(string? value, string text)
    {
        return value?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static IOrderedEnumerable<TaskItem> SortTaskItems(
        IEnumerable<TaskItem> taskItems,
        TaskItemQuery query)
    {
        return query.SortField switch
        {
            TaskItemSortField.CreatedAt => query.SortDescending
                ? taskItems.OrderByDescending(taskItem => taskItem.CreatedAt)
                : taskItems.OrderBy(taskItem => taskItem.CreatedAt),
            TaskItemSortField.FollowUpAt => query.SortDescending
                ? taskItems.OrderByDescending(taskItem => taskItem.FollowUpAt ?? DateTimeOffset.MinValue)
                : taskItems.OrderBy(taskItem => taskItem.FollowUpAt ?? DateTimeOffset.MaxValue),
            TaskItemSortField.Title => query.SortDescending
                ? taskItems.OrderByDescending(taskItem => taskItem.Title)
                : taskItems.OrderBy(taskItem => taskItem.Title),
            TaskItemSortField.Status => query.SortDescending
                ? taskItems.OrderByDescending(taskItem => taskItem.Status ?? string.Empty)
                : taskItems.OrderBy(taskItem => taskItem.Status ?? string.Empty),
            _ => query.SortDescending
                ? taskItems.OrderByDescending(taskItem => taskItem.LastTouchedAt)
                : taskItems.OrderBy(taskItem => taskItem.LastTouchedAt)
        };
    }
}
