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
        CancellationToken cancellationToken)
    {
        var query = _dbContext.TaskItems
            .AsNoTracking()
            .Where(taskItem =>
                taskItem.WorkspaceId == workspaceId &&
                taskItem.ProjectId == projectId &&
                taskItem.ArchivedAt == null);

        if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            return (await query.ToListAsync(cancellationToken))
                .OrderByDescending(taskItem => taskItem.LastTouchedAt)
                .ThenBy(taskItem => taskItem.Title)
                .ToList();
        }

        return await query
            .OrderByDescending(taskItem => taskItem.LastTouchedAt)
            .ThenBy(taskItem => taskItem.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskItem?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.TaskItems
            .Include("_fieldValues")
            .Include("_timelineEntries")
            .Where(taskItem =>
                taskItem.Id == id &&
                taskItem.WorkspaceId == workspaceId &&
                taskItem.ProjectId == projectId);

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
}
