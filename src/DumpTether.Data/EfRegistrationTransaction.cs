using DumpTether.App.Auth;
using Microsoft.EntityFrameworkCore.Storage;

namespace DumpTether.Data;

internal sealed class EfRegistrationTransaction : IRegistrationTransaction
{
    private readonly DumpTetherDbContext _dbContext;

    public EfRegistrationTransaction(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(CancellationToken.None);
            return result;
        }
        catch
        {
            await RollBackAsync(transaction);
            throw;
        }
    }

    private static async Task RollBackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Preserve the registration failure that caused the rollback.
        }
    }
}
