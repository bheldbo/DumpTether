using System.Text.Json.Serialization;
using DumpTether.Domain;

namespace DumpTether.App.Workspaces;

public sealed record UpdateWorkspaceRequest(
    string? Name = null,
    string? Color = null);

public sealed record CreateWorkspaceRequest(
    string Name,
    string? Color = null);

public sealed record CreateWorkspaceInvitationRequest(
    string Email,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    WorkspaceMembershipRole Role = WorkspaceMembershipRole.Member);

public sealed record AcceptWorkspaceInvitationRequest(
    string? Token = null,
    Guid? InvitationId = null);
