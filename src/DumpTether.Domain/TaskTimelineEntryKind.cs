namespace DumpTether.Domain;

public enum TaskTimelineEntryKind
{
    Created = 1,
    NoteAdded = 2,
    StatusChanged = 3,
    Archived = 4,
    TitleChanged = 5,
    FollowUpChanged = 6,
    FieldValueChanged = 7,
    Reopened = 8,
    CategoryChanged = 9,
    ColorChanged = 10
}
