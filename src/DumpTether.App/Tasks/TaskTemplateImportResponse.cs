using DumpTether.App.Templates;

namespace DumpTether.App.Tasks;

public sealed record TaskTemplateImportResponse(
    Guid SourceTemplateId,
    TaskTemplateDetailResponse Template);
