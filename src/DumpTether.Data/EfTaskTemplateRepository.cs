using DumpTether.App.Templates;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfTaskTemplateRepository : ITaskTemplateRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfTaskTemplateRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TaskTemplate taskTemplate, CancellationToken cancellationToken)
    {
        await _dbContext.TaskTemplates.AddAsync(taskTemplate, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskTemplate>> ListAsync(
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.TaskTemplates
            .AsNoTracking()
            .Include("_fieldDefinitions")
            .AsSplitQuery()
            .Where(template =>
                template.OwnerUserId == ownerUserId &&
                template.DeletedAt == null)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskTemplate?> GetByIdAsync(
        Guid id,
        Guid? ownerUserId,
        bool trackChanges,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.TaskTemplates
            .Include("_fieldDefinitions")
            .AsSplitQuery()
            .Where(template =>
                template.Id == id &&
                template.OwnerUserId == ownerUserId);

        if (!includeDeleted)
        {
            query = query.Where(template => template.DeletedAt == null);
        }

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyActiveWithNameAsync(
        Guid? ownerUserId,
        string name,
        Guid? excludedTemplateId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.TaskTemplates
            .AnyAsync(
                template =>
                    template.OwnerUserId == ownerUserId &&
                    template.DeletedAt == null &&
                    template.Name == name &&
                    (!excludedTemplateId.HasValue || template.Id != excludedTemplateId.Value),
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TaskTemplate>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, TaskTemplate>();
        }

        var templates = await _dbContext.TaskTemplates
            .AsNoTracking()
            .Include("_fieldDefinitions")
            .AsSplitQuery()
            .Where(template => ids.Contains(template.Id))
            .ToListAsync(cancellationToken);

        return templates.ToDictionary(template => template.Id);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
