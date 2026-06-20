import {
  FormEvent,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { Icon } from '../../components/Icon';
import { toFieldValueMap } from '../../fieldValues';
import { type Translate } from '../../localization';
import {
  getTemplateLayoutCellStyle,
  getTemplateLayoutRows,
} from '../../templateLayout';
import {
  entryFieldsHaveContent,
  fieldValueIsEmpty,
  withDefaultFieldValues,
} from '../../templateFieldUtils';
import type {
  FieldDefinitionResponse,
  FieldValueMap,
  FieldValuePrimitive,
  TaskItemDetailResponse,
  TaskTemplateDetailResponse,
} from '../../types';
import { formatDateTime } from '../../appUtils';

export function TimelinePanel({
  entryFields,
  onAddTimelineEntry,
  onQueueDeleteTimelineEntry,
  onUndoDeleteTimelineEntry,
  onUpdateTimelineEntry,
  pendingDeletedNoteIds,
  t,
  timelineEntries,
}: {
  entryFields: TaskTemplateDetailResponse['fields'];
  onAddTimelineEntry: (note: string, fieldValues?: FieldValueMap) => Promise<void>;
  onQueueDeleteTimelineEntry: (entryId: string) => void;
  onUndoDeleteTimelineEntry: (entryId: string) => void;
  onUpdateTimelineEntry: (
    entryId: string,
    note: string | null,
    fieldValues?: FieldValueMap,
  ) => Promise<void>;
  pendingDeletedNoteIds: string[];
  t: Translate;
  timelineEntries: TaskItemDetailResponse['timelineEntries'];
}) {
  const notes = timelineEntries.filter((entry) => entry.kind === 'NoteAdded');

  return (
    <section className="timeline-panel notes-panel" aria-labelledby="timeline-title">
      <div className="section-heading">
        <h3 id="timeline-title">{t('notes')}</h3>
        <span>{notes.length} {t('noteCount')}</span>
      </div>

      <AddTimelineEntryForm
        entryFields={entryFields}
        onAddTimelineEntry={onAddTimelineEntry}
        t={t}
      />

      <ol className="timeline-list">
        {notes.length === 0 ? <li className="empty-copy">{t('noNotesYet')}</li> : null}
        {notes.map((entry) => (
          <NoteEntry
            entry={entry}
            entryFields={entryFields}
            isPendingDelete={pendingDeletedNoteIds.includes(entry.id)}
            key={entry.id}
            onQueueDeleteTimelineEntry={onQueueDeleteTimelineEntry}
            onUndoDeleteTimelineEntry={onUndoDeleteTimelineEntry}
            onUpdateTimelineEntry={onUpdateTimelineEntry}
            t={t}
          />
        ))}
      </ol>
    </section>
  );
}

function NoteEntry({
  entry,
  entryFields,
  isPendingDelete,
  onQueueDeleteTimelineEntry,
  onUndoDeleteTimelineEntry,
  onUpdateTimelineEntry,
  t,
}: {
  entry: TaskItemDetailResponse['timelineEntries'][number];
  entryFields: TaskTemplateDetailResponse['fields'];
  isPendingDelete: boolean;
  onQueueDeleteTimelineEntry: (entryId: string) => void;
  onUndoDeleteTimelineEntry: (entryId: string) => void;
  onUpdateTimelineEntry: (
    entryId: string,
    note: string | null,
    fieldValues?: FieldValueMap,
  ) => Promise<void>;
  t: Translate;
}) {
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(entry.details ?? '');
  const [isConfirmingDelete, setIsConfirmingDelete] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [entryFieldsAreSaving, setEntryFieldsAreSaving] = useState(false);
  const editContainerRef = useRef<HTMLDivElement>(null);
  const hasEntryFields = entryFields.length > 0;

  useEffect(() => {
    setDraft(entry.details ?? '');
    setIsEditing(false);
    setIsConfirmingDelete(false);
    setEntryFieldsAreSaving(false);
  }, [entry]);

  const cancelEdit = () => {
    setDraft(entry.details ?? '');
    setIsEditing(false);
  };

  const save = async () => {
    const trimmedDraft = draft.trim();
    if (!trimmedDraft) {
      cancelEdit();
      return;
    }

    setIsSubmitting(true);
    await onUpdateTimelineEntry(
      entry.id,
      trimmedDraft,
    );
    setIsSubmitting(false);
    setIsEditing(false);
  };

  return (
    <li className="note-entry" data-pending-delete={isPendingDelete}>
      <span className="note-entry-time">
        <time dateTime={entry.occurredAt}>{formatDateTime(entry.occurredAt)}</time>
        {entryFieldsAreSaving || isSubmitting ? (
          <span aria-label="Saving" className="entry-saving-copy" role="status" />
        ) : null}
      </span>
      {hasEntryFields ? (
        <InlineEntryFieldRow
          entry={entry}
          fields={entryFields}
          onSavingChange={setEntryFieldsAreSaving}
          onUpdateTimelineEntry={onUpdateTimelineEntry}
        />
      ) : isEditing ? (
        <div
          className="note-edit"
          data-saving={isSubmitting}
          onBlur={(event) => {
            const nextTarget = event.relatedTarget;
            if (
              nextTarget instanceof Node &&
              event.currentTarget.contains(nextTarget)
            ) {
              return;
            }

            void save();
          }}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.preventDefault();
              cancelEdit();
            }
          }}
          ref={editContainerRef}
        >
          <textarea
            autoFocus
            aria-label={t('note')}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void save();
              }
            }}
            rows={3}
            value={draft}
          />
        </div>
      ) : entry.details ? (
        <button className="note-body" onClick={() => setIsEditing(true)} type="button">
          {entry.details}
        </button>
      ) : (
        <span className="note-body note-body-empty">{t('note')}</span>
      )}
      <div className="note-delete-cell">
        {isPendingDelete ? (
          <button
            className="icon-button note-undo-button"
            onClick={() => onUndoDeleteTimelineEntry(entry.id)}
            title={t('undo')}
            type="button"
          >
            <Icon name="undo" />
            <span className="sr-only">{t('undo')}</span>
          </button>
        ) : isConfirmingDelete ? (
          <div className="note-confirm-delete">
            <span>{t('confirmDelete')}</span>
            <button
              className="icon-button"
              onClick={() => {
                onQueueDeleteTimelineEntry(entry.id);
                setIsConfirmingDelete(false);
              }}
              title={t('deleteNote')}
              type="button"
            >
              <Icon name="check" />
            </button>
            <button
              className="icon-button"
              onClick={() => setIsConfirmingDelete(false)}
              title={t('keep')}
              type="button"
            >
              <Icon name="close" />
            </button>
          </div>
        ) : (
          <button
            className="icon-button note-delete-button"
            onClick={() => setIsConfirmingDelete(true)}
            title={t('deleteNote')}
            type="button"
          >
            <Icon name="close" />
          </button>
        )}
      </div>
    </li>
  );
}

function InlineEntryFieldRow({
  entry,
  fields,
  onSavingChange,
  onUpdateTimelineEntry,
}: {
  entry: TaskItemDetailResponse['timelineEntries'][number];
  fields: FieldDefinitionResponse[];
  onSavingChange: (isSaving: boolean) => void;
  onUpdateTimelineEntry: (
    entryId: string,
    note: string | null,
    fieldValues?: FieldValueMap,
  ) => Promise<void>;
}) {
  const [fieldDraft, setFieldDraft] = useState<FieldValueMap>(
    () => withDefaultFieldValues(fields, toFieldValueMap(entry.fieldValues)),
  );
  const [isSaving, setIsSaving] = useState(false);
  const saveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastSavedValuesRef = useRef('');
  const note = entry.details?.trim() || null;

  useEffect(() => {
    const nextValues = withDefaultFieldValues(fields, toFieldValueMap(entry.fieldValues));
    setFieldDraft(nextValues);
    lastSavedValuesRef.current = JSON.stringify(nextValues);
  }, [entry.fieldValues, fields]);

  useEffect(() => () => {
    if (saveTimerRef.current) {
      clearTimeout(saveTimerRef.current);
    }
  }, []);

  const saveValues = useCallback(
    async (values: FieldValueMap) => {
      const nextValues = withDefaultFieldValues(fields, values);
      const serializedValues = JSON.stringify(nextValues);

      if (serializedValues === lastSavedValuesRef.current) {
        return;
      }

      if (saveTimerRef.current) {
        clearTimeout(saveTimerRef.current);
      }

      setIsSaving(true);
      onSavingChange(true);
      try {
        await onUpdateTimelineEntry(entry.id, note, nextValues);
        lastSavedValuesRef.current = serializedValues;
      } finally {
        setIsSaving(false);
        onSavingChange(false);
      }
    },
    [entry.id, fields, note, onSavingChange, onUpdateTimelineEntry],
  );

  const scheduleSave = useCallback(
    (values: FieldValueMap, immediate = false) => {
      if (saveTimerRef.current) {
        clearTimeout(saveTimerRef.current);
      }

      if (immediate) {
        void saveValues(values);
        return;
      }

      saveTimerRef.current = setTimeout(() => {
        void saveValues(values);
      }, 450);
    },
    [saveValues],
  );

  const updateField = (field: FieldDefinitionResponse, value: FieldValuePrimitive) => {
    const nextValues = {
      ...fieldDraft,
      [field.id]: value,
    };

    setFieldDraft(nextValues);
    scheduleSave(nextValues, field.type === 'Checkbox' || field.type === 'Select');
  };

  return (
    <div
      className="entry-inline-edit"
      data-saving={isSaving}
      onBlur={(event) => {
        const nextTarget = event.relatedTarget;

        if (
          nextTarget instanceof Node &&
          event.currentTarget.contains(nextTarget)
        ) {
          return;
        }

        void saveValues(fieldDraft);
      }}
    >
      <EntryFieldEditorRow
        fields={fields}
        onChange={updateField}
        values={fieldDraft}
      />
    </div>
  );
}

function EntryFieldEditorRow({
  fields,
  onChange,
  values,
}: {
  fields: FieldDefinitionResponse[];
  onChange: (field: FieldDefinitionResponse, value: FieldValuePrimitive) => void;
  values: FieldValueMap;
}) {
  const layoutRows = getTemplateLayoutRows(fields);

  return (
    <div className="entry-field-editor-layout">
      {layoutRows.map((row) => (
        <div className="entry-field-editor-row" key={row.row} style={row.style}>
          {row.fields.map((field) => (
            <label
              className="entry-field-editor-cell"
              data-field-type={field.type}
              data-empty={fieldValueIsEmpty(values[field.id] ?? null)}
              key={field.id}
              style={getTemplateLayoutCellStyle(field)}
            >
              <EntryFieldControl
                field={field}
                onChange={(value) => onChange(field, value)}
                value={values[field.id] ?? (field.type === 'Checkbox' ? false : '')}
              />
            </label>
          ))}
        </div>
      ))}
    </div>
  );
}

function EntryFieldControl({
  field,
  onChange,
  value,
}: {
  field: FieldDefinitionResponse;
  onChange: (value: FieldValuePrimitive) => void;
  value: FieldValuePrimitive;
}) {
  const label = field.required ? `${field.name} *` : field.name;

  switch (field.type) {
    case 'Checkbox':
      return (
        <span className="entry-checkbox-display">
          <input
            aria-label={label}
            checked={value === true}
            onChange={(event) => onChange(event.target.checked)}
            type="checkbox"
          />
          <span>{label}</span>
        </span>
      );
    case 'Date':
      return (
        <input
          aria-label={label}
          onChange={(event) => onChange(event.target.value || null)}
          placeholder={field.name}
          required={field.required}
          type="date"
          value={typeof value === 'string' ? value.slice(0, 10) : ''}
        />
      );
    case 'Select':
      return (
        <select
          aria-label={label}
          onChange={(event) => onChange(event.target.value || null)}
          required={field.required}
          value={typeof value === 'string' ? value : ''}
        >
          <option value="">{field.name}</option>
          {field.options.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      );
    case 'LongText':
      return (
        <textarea
          aria-label={label}
          onChange={(event) => onChange(event.target.value)}
          placeholder={label}
          required={field.required}
          rows={2}
          value={typeof value === 'string' ? value : ''}
        />
      );
    case 'Text':
    default:
      return (
        <input
          aria-label={label}
          onChange={(event) => onChange(event.target.value)}
          placeholder={label}
          required={field.required}
          type="text"
          value={typeof value === 'string' ? value : ''}
        />
      );
  }
}

function AddTimelineEntryForm({
  entryFields,
  onAddTimelineEntry,
  t,
}: {
  entryFields: TaskTemplateDetailResponse['fields'];
  onAddTimelineEntry: (note: string, fieldValues?: FieldValueMap) => Promise<void>;
  t: Translate;
}) {
  const [note, setNote] = useState('');
  const [fieldValues, setFieldValues] = useState<FieldValueMap>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const hasEntryFields = entryFields.length > 0;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedNote = note.trim();
    const entryFieldValues = hasEntryFields
      ? withDefaultFieldValues(entryFields, fieldValues)
      : undefined;

    if (
      !trimmedNote &&
      (!entryFieldValues || !entryFieldsHaveContent(entryFields, fieldValues))
    ) {
      return;
    }

    setIsSubmitting(true);
    await onAddTimelineEntry(trimmedNote, entryFieldValues);
    setNote('');
    setFieldValues({});
    setIsSubmitting(false);
    textareaRef.current?.focus();
  };

  return (
    <form className="timeline-form" onSubmit={handleSubmit}>
      {hasEntryFields ? (
        <EntryFieldEditorRow
          fields={entryFields}
          onChange={(field, value) =>
            setFieldValues((currentValues) => ({
              ...currentValues,
              [field.id]: value,
            }))
          }
          values={fieldValues}
        />
      ) : (
        <textarea
          aria-label={t('note')}
          ref={textareaRef}
          onChange={(event) => setNote(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && !event.shiftKey) {
              event.preventDefault();
              event.currentTarget.form?.requestSubmit();
            }
          }}
          placeholder={t('addNotePlaceholder')}
          rows={3}
          value={note}
        />
      )}
      <button
        disabled={
          (!note.trim() &&
            (!hasEntryFields || !entryFieldsHaveContent(entryFields, fieldValues))) ||
          isSubmitting
        }
        type="submit"
      >
        <Icon name="note" />
        <span>{t('note')}</span>
      </button>
    </form>
  );
}
