namespace DumpTether.Domain;

public enum SyncRootStatus
{
    LocalOnly = 1,
    Linked = 2,
    Conflict = 3,
    AccessRevoked = 4
}
