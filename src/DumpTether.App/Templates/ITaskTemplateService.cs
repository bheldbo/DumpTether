namespace DumpTether.App.Templates;

public interface ITaskTemplateService
{
    Task<IReadOnlyList<TaskTemplateSummaryResponse>> ListAsync(
        CancellationToken cancellationToken);

    Task<TaskTemplateDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<TaskTemplateDetailResponse> CreateAsync(
        CreateTaskTemplateRequest request,
        CancellationToken cancellationToken);

    Task<TaskTemplateDetailResponse?> UpdateAsync(
        Guid id,
        UpdateTaskTemplateRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
