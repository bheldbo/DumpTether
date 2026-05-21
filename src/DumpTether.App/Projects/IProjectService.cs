namespace DumpTether.App.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken cancellationToken);
}
