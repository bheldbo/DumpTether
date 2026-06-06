using DumpTether.App.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace DumpTether.Api;

internal sealed class WorkspaceWriteRequirement : IAuthorizationRequirement;

internal sealed class WorkspaceWriteAuthorizationHandler
    : AuthorizationHandler<WorkspaceWriteRequirement>
{
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly ILogger<WorkspaceWriteAuthorizationHandler> _logger;

    public WorkspaceWriteAuthorizationHandler(
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        ILogger<WorkspaceWriteAuthorizationHandler> logger)
    {
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WorkspaceWriteRequirement requirement)
    {
        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        try
        {
            var workspace = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);

            if (workspace.CanWriteWorkspace)
            {
                context.Succeed(requirement);
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException ||
            exception is InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Workspace write authorization failed.");
        }
    }
}
