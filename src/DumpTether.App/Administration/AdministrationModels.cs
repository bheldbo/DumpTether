using DumpTether.Domain;

namespace DumpTether.App.Administration;

public sealed record AdministrationUserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTimeOffset? EmailConfirmedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    int ActiveSessionCount,
    int OwnedBoardCount,
    int MembershipCount);

public sealed record AdministrationSessionSummary(
    Guid Id,
    UserSessionType SessionType,
    string? DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? RevokedAt);

public sealed record AdministrationUserDetails(
    AdministrationUserSummary User,
    IReadOnlyList<AdministrationSessionSummary> Sessions);

public sealed record AccountDeletionResult(
    string Email,
    int DeletedBoardCount,
    int DeletedSessionCount,
    int DeletedShareCount,
    int DeletedTemplateCount,
    int PreservedTemplateCount);

public sealed record AdministrationStatistics(
    int RegisteredUserCount,
    int ActiveUserCount,
    int ConfirmedUserCount,
    int ActiveSessionCount,
    int RecentlySeenSessionCount,
    int BoardCount,
    int ActiveTaskCount,
    int ArchivedTaskCount,
    DateTimeOffset GeneratedAt);
