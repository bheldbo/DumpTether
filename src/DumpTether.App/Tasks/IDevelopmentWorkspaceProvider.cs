namespace DumpTether.App.Tasks;

public interface IDevelopmentWorkspaceProvider
{
    Task<DevelopmentWorkspaceContext> GetCurrentAsync(CancellationToken cancellationToken);
}
