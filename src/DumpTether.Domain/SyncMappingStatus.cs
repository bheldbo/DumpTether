namespace DumpTether.Domain;

public enum SyncMappingStatus
{
    LocalOnly = 1,
    Synced = 2,
    Conflict = 3,
    Deleted = 4,
    SyncFailed = 5
}
