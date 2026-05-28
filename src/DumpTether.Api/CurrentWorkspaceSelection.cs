using DumpTether.App.Workspaces;

namespace DumpTether.Api;

internal sealed class CurrentWorkspaceSelection : ICurrentWorkspaceSelection
{
    private const string HeaderName = "X-DumpTether-Workspace-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentWorkspaceSelection(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? WorkspaceId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var rawValue = httpContext?.Request.Headers[HeaderName].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                rawValue = httpContext?.Request.Query["workspaceId"].FirstOrDefault();
            }

            return Guid.TryParse(rawValue, out var workspaceId)
                ? workspaceId
                : null;
        }
    }
}
