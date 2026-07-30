namespace DumpTether.App.LiveUpdates;

public static class LiveUpdateEvents
{
    public const string TaskCreated = nameof(TaskCreated);
    public const string TaskUpdated = nameof(TaskUpdated);
    public const string NoteAdded = nameof(NoteAdded);
    public const string NoteEdited = nameof(NoteEdited);
    public const string NoteDeleted = nameof(NoteDeleted);
    public const string TaskShared = nameof(TaskShared);
    public const string WorkspaceCreated = nameof(WorkspaceCreated);
    public const string WorkspaceUpdated = nameof(WorkspaceUpdated);
    public const string WorkspaceDeleted = nameof(WorkspaceDeleted);
    public const string WorkspaceInviteAccepted = nameof(WorkspaceInviteAccepted);
}
