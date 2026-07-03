import {
  type KeyboardEvent,
  type MouseEvent,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { ColorPickerPopover } from '../../components/ColorPickerPopover';
import { Icon } from '../../components/Icon';
import { TaskSyncIndicator } from '../../components/TaskSyncIndicator';
import {
  formatFullDate,
  formatRelativeDate,
  toDateInputValue,
} from '../../appUtils';
import { FieldEditorList, FieldValueList } from '../../fieldRenderers';
import { toFieldValueMap } from '../../fieldValues';
import { type Translate } from '../../localization';
import {
  getContextChipStyle,
  getFollowUpTone,
  getPrimaryProjectIdForCategories,
  getProjectsForTaskCategories,
  getTaskCardStyle,
  joinTaskCategories,
  splitTaskCategories,
} from '../../taskUtils';
import { withDefaultFieldValues } from '../../templateFieldUtils';
import { TimelinePanel } from '../timeline/TimelinePanel';
import { TaskShareStrip } from '../sharing/ShareDialog';
import { ArchiveDialog } from './TaskDialogs';
import { CategoryMultiSelect } from './CategoryMultiSelect';
import type {
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  CreateTaskShareRequest,
  FieldValueMap,
  ProjectResponse,
  TaskItemDetailResponse,
  TaskShareLinkResponse,
  UpdateTaskItemRequest,
  UpdateTaskShareRequest,
} from '../../types';

interface TaskDetailProps {
  archiveDialogIsOpen: boolean;
  archiveResolutions: ArchiveResolutionResponse[];
  canManageSharing: boolean;
  colorOptions: string[];
  onAddTimelineEntry: (note: string, fieldValues?: FieldValueMap) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onClose: () => Promise<void>;
  onCloseArchiveDialog: () => void;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onCreateTaskShareLink: (
    taskItemId: string,
    requestBody: CreateTaskShareRequest,
  ) => Promise<TaskShareLinkResponse>;
  onQueueDeleteTimelineEntry: (entryId: string) => void;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onUndoDeleteTimelineEntry: (entryId: string) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTaskShareRole: (
    taskItemId: string,
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => Promise<TaskItemDetailResponse>;
  onUpdateTimelineEntry: (
    entryId: string,
    note: string | null,
    fieldValues?: FieldValueMap,
  ) => Promise<void>;
  pendingDeletedNoteIds: string[];
  projects: ProjectResponse[];
  statusOptions: string[];
  t: Translate;
  taskItem: TaskItemDetailResponse;
}

export function TaskDetail({
  archiveDialogIsOpen,
  archiveResolutions,
  canManageSharing,
  colorOptions,
  onAddTimelineEntry,
  onArchive,
  onClose,
  onCloseArchiveDialog,
  onOpenArchiveDialog,
  onReopen,
  onCreateTaskShareLink,
  onQueueDeleteTimelineEntry,
  onRevokeTaskShare,
  onUndoDeleteTimelineEntry,
  onUpdateFieldValues,
  onUpdateTaskItem,
  onUpdateTaskShareRole,
  onUpdateTimelineEntry,
  pendingDeletedNoteIds,
  projects,
  statusOptions,
  t,
  taskItem,
}: TaskDetailProps) {
  const [reopenNote, setReopenNote] = useState('');
  const [fieldDraft, setFieldDraft] = useState<FieldValueMap>({});
  const [isSavingFields, setIsSavingFields] = useState(false);
  const fieldSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastSavedFieldDraftRef = useRef('');
  const headerFields = useMemo(
    () => taskItem.template?.fields.filter((field) => field.scope === 'Header') ?? [],
    [taskItem.template],
  );
  const entryFields = useMemo(
    () => taskItem.template?.fields.filter((field) => field.scope === 'Entry') ?? [],
    [taskItem.template],
  );

  useEffect(() => {
    setReopenNote('');
    const nextFieldDraft = toFieldValueMap(taskItem.fieldValues);
    setFieldDraft(nextFieldDraft);
    lastSavedFieldDraftRef.current = JSON.stringify(
      withDefaultFieldValues(headerFields, nextFieldDraft),
    );
  }, [taskItem, headerFields]);

  const headerFieldsCanBeEdited = !taskItem.archivedAt && headerFields.length > 0;

  useEffect(() => {
    if (!headerFieldsCanBeEdited) {
      return undefined;
    }

    const nextFieldValues = withDefaultFieldValues(headerFields, fieldDraft);
    const serializedValues = JSON.stringify(nextFieldValues);

    if (serializedValues === lastSavedFieldDraftRef.current) {
      return undefined;
    }

    if (fieldSaveTimerRef.current) {
      clearTimeout(fieldSaveTimerRef.current);
    }

    fieldSaveTimerRef.current = setTimeout(() => {
      setIsSavingFields(true);
      void onUpdateFieldValues(nextFieldValues)
        .then(() => {
          lastSavedFieldDraftRef.current = serializedValues;
        })
        .finally(() => {
          setIsSavingFields(false);
        });
    }, 500);

    return () => {
      if (fieldSaveTimerRef.current) {
        clearTimeout(fieldSaveTimerRef.current);
      }
    };
  }, [fieldDraft, headerFields, headerFieldsCanBeEdited, onUpdateFieldValues]);

  const closeFromHeader = (event: MouseEvent<HTMLDivElement>) => {
    if (
      event.target instanceof HTMLElement &&
      event.target.closest(
        'button, input, select, textarea, label, .color-popover, .task-share-popover, .share-dialog, .task-header-fields, .task-meta-chip, .member-chip, .share-chip, .pending-invite-chip, .category-multi-select',
      )
    ) {
      return;
    }

    void onClose();
  };

  return (
    <section className="task-detail" aria-label="Task detail">
      <div
        className="detail-header task-detail-header"
        onClick={closeFromHeader}
        style={getTaskCardStyle(taskItem.color)}
      >
        <button
          className="icon-button task-detail-back-button"
          onClick={() => void onClose()}
          title={t('backToWall')}
          type="button"
        >
          <Icon name="back" />
          <span className="sr-only">{t('backToWall')}</span>
        </button>
        <TaskHeaderEditor
          onUpdateTaskItem={onUpdateTaskItem}
          projects={projects}
          statusOptions={statusOptions}
          t={t}
          taskItem={taskItem}
        />

        <div className="detail-actions">
          <TaskSyncIndicator syncState={taskItem.syncState} t={t} />
          {!taskItem.archivedAt ? (
            <ColorPickerPopover
              color={taskItem.color ?? ''}
              colorOptions={colorOptions}
              label={t('taskColor')}
              onChange={(color) => void onUpdateTaskItem({ color })}
              placement="leftWide"
              t={t}
            />
          ) : null}
          {taskItem.archivedAt ? (
            <form
              className="reopen-form"
              onSubmit={(event) => {
                event.preventDefault();
                void onReopen(reopenNote.trim() || undefined);
              }}
            >
              <input
                aria-label="Reopen note"
                onChange={(event) => setReopenNote(event.target.value)}
                placeholder="Optional reopen note"
                type="text"
                value={reopenNote}
              />
              <button type="submit">Reopen</button>
            </form>
          ) : (
            <button className="secondary-action" onClick={onOpenArchiveDialog} type="button">
              <Icon name="archive" />
              <span>{t('archiveAction')}</span>
            </button>
          )}
        </div>
        {canManageSharing ? (
          <div className="task-detail-share-corner">
            <TaskShareStrip
              onCreateTaskShareLink={onCreateTaskShareLink}
              onRevokeTaskShare={onRevokeTaskShare}
              onUpdateTaskShareRole={onUpdateTaskShareRole}
              t={t}
              taskItem={taskItem}
            />
          </div>
        ) : null}
      </div>

      {headerFields.length > 0 ? (
        <section className="detail-section fields-details task-header-fields-section">
          <div className="section-heading">
            <span>
              <h3 id="fields-title">{t('taskFields')}</h3>
            </span>
            {isSavingFields ? (
              <span
                aria-label={t('saving')}
                className="fields-saving saving-copy"
                data-state="saving"
                role="status"
                title={t('saving')}
              />
            ) : null}
          </div>

          {headerFieldsCanBeEdited ? (
            <FieldEditorList
              fields={headerFields}
              layoutRows={taskItem.template?.layout.header ?? []}
              onChange={(fieldId, value) =>
                setFieldDraft((currentValues) => ({
                  ...currentValues,
                  [fieldId]: value,
                }))
              }
              values={fieldDraft}
            />
          ) : (
            <FieldValueList
              fields={headerFields}
              fieldValues={taskItem.fieldValues}
              layoutRows={taskItem.template?.layout.header ?? []}
            />
          )}
        </section>
      ) : null}

      <TimelinePanel
        entryFields={entryFields}
        entryLayoutRows={taskItem.template?.layout.entry ?? []}
        onAddTimelineEntry={onAddTimelineEntry}
        onQueueDeleteTimelineEntry={onQueueDeleteTimelineEntry}
        onUndoDeleteTimelineEntry={onUndoDeleteTimelineEntry}
        onUpdateTimelineEntry={onUpdateTimelineEntry}
        pendingDeletedNoteIds={pendingDeletedNoteIds}
        t={t}
        timelineEntries={taskItem.timelineEntries}
      />

      {archiveDialogIsOpen ? (
        <ArchiveDialog
          archiveResolutions={archiveResolutions}
          onArchive={onArchive}
          onClose={onCloseArchiveDialog}
          t={t}
          taskTitle={taskItem.title}
        />
      ) : null}
    </section>
  );
}

function TaskHeaderEditor({
  onUpdateTaskItem,
  projects,
  statusOptions,
  t,
  taskItem,
}: {
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  projects: ProjectResponse[];
  statusOptions: string[];
  t: Translate;
  taskItem: TaskItemDetailResponse;
}) {
  const [title, setTitle] = useState(taskItem.title);
  const [status, setStatus] = useState(taskItem.status ?? '');
  const [category, setCategory] = useState(taskItem.category ?? '');
  const [categoryProjectId, setCategoryProjectId] = useState(taskItem.projectId ?? '');
  const [followUpDate, setFollowUpDate] = useState(toDateInputValue(taskItem.followUpAt));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [saveState, setSaveState] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const [editingField, setEditingField] = useState<
    'title' | 'status' | 'category' | 'followUp' | null
  >(null);
  const editingFieldRef = useRef<typeof editingField>(null);
  const selectedCategoryNames = splitTaskCategories(category);
  const displayedProjects = getProjectsForTaskCategories(taskItem.category ?? category, projects);
  const displayedProject = taskItem.projectId
    ? projects.find((project) => project.id === taskItem.projectId) ?? displayedProjects[0] ?? null
    : displayedProjects[0] ?? null;
  const displayedCategoryLabel = splitTaskCategories(taskItem.category).join(', ') || t('noCategory');

  useEffect(() => {
    setTitle(taskItem.title);
    setStatus(taskItem.status ?? '');
    setCategory(taskItem.category ?? '');
    const taskCategoryNames = splitTaskCategories(taskItem.category);
    setCategoryProjectId(
      taskItem.projectId ??
      projects.find((project) =>
        taskCategoryNames.some((categoryName) =>
          categoryName.toLowerCase() === project.name.toLowerCase()))?.id ??
      '',
    );
    setFollowUpDate(toDateInputValue(taskItem.followUpAt));
    setSaveState('idle');
  }, [projects, taskItem]);

  useEffect(() => {
    setEditingField(null);
  }, [taskItem.id]);

  useEffect(() => {
    editingFieldRef.current = editingField;
  }, [editingField]);

  const clearEditingField = (field?: typeof editingField) => {
    if (!field || editingFieldRef.current === field) {
      setEditingField(null);
    }
  };

  const saveChanges = async (overrides: Partial<{
    title: string;
    status: string;
    category: string;
    projectId: string | null;
    followUpDate: string;
  }> = {}, options: { field?: typeof editingField; keepEditing?: boolean } = {}) => {
    if (taskItem.archivedAt) {
      return;
    }

    const nextTitle = (overrides.title ?? title).trim();
    const nextStatus = (overrides.status ?? status).trim();
    const nextCategory = joinTaskCategories(splitTaskCategories(overrides.category ?? category)) ?? '';
    const nextProjectId = Object.prototype.hasOwnProperty.call(overrides, 'projectId')
      ? overrides.projectId
      : Object.prototype.hasOwnProperty.call(overrides, 'category')
        ? getPrimaryProjectIdForCategories(nextCategory, projects)
        : categoryProjectId;
    const nextFollowUpDate = overrides.followUpDate ?? followUpDate;
    const normalizedFollowUpAt = nextFollowUpDate
      ? new Date(`${nextFollowUpDate}T12:00:00`).toISOString()
      : null;
    const normalizedNextProjectId = nextProjectId ?? '';
    const normalizedCurrentProjectId = taskItem.projectId ?? '';

    if (!nextTitle) {
      setTitle(taskItem.title);
      return;
    }

    const hasChanges =
      nextTitle !== taskItem.title ||
      nextStatus !== (taskItem.status ?? '') ||
      nextCategory !== (joinTaskCategories(splitTaskCategories(taskItem.category)) ?? '') ||
      normalizedNextProjectId !== normalizedCurrentProjectId ||
      normalizedFollowUpAt !== taskItem.followUpAt;

    if (!hasChanges) {
      if (!options.keepEditing) {
        clearEditingField(options.field);
      }
      return;
    }

    setSaveState('saving');
    setIsSubmitting(true);
    try {
      await onUpdateTaskItem({
        title: nextTitle,
        status: nextStatus,
        category: nextCategory,
        projectId: nextProjectId || null,
        followUpAt: normalizedFollowUpAt,
      });
      setSaveState('saved');
      setTitle(nextTitle);
      setStatus(nextStatus);
      setCategory(nextCategory);
      setCategoryProjectId(nextProjectId ?? '');
      setFollowUpDate(nextFollowUpDate);
      if (!options.keepEditing) {
        clearEditingField(options.field);
      }
    } catch {
      setSaveState('error');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTextKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.currentTarget.blur();
    }

    if (event.key === 'Escape') {
      setTitle(taskItem.title);
      setStatus(taskItem.status ?? '');
      setCategory(taskItem.category ?? '');
      setCategoryProjectId(
        taskItem.projectId ??
        getPrimaryProjectIdForCategories(taskItem.category ?? '', projects) ??
        '',
      );
      setFollowUpDate(toDateInputValue(taskItem.followUpAt));
      setEditingField(null);
      event.currentTarget.blur();
    }
  };

  if (taskItem.archivedAt) {
    return (
      <div className="task-header-editor">
        <p className="detail-kicker">{t('archivedTask')}</p>
        <h2>{taskItem.title}</h2>
        <div className="task-header-fields">
          <span>{t('created')}: {formatFullDate(taskItem.createdAt)}</span>
          <span title={`${t('lastUpdated')}: ${formatRelativeDate(taskItem.lastTouchedAt)}`}>
            {t('lastUpdated')}: {formatRelativeDate(taskItem.lastTouchedAt)}
          </span>
          <span>{t('status')}: {taskItem.status ?? t('noStatus')}</span>
          {splitTaskCategories(taskItem.category).length > 0 ? (
            splitTaskCategories(taskItem.category).map((categoryName) => {
              const project = projects.find((candidate) =>
                candidate.name.toLowerCase() === categoryName.toLowerCase()) ?? null;

              return (
                <span key={categoryName} style={getContextChipStyle(project?.color ?? null)}>
                  {t('category')}: {categoryName}
                </span>
              );
            })
          ) : (
            <span>{t('category')}: {t('noCategory')}</span>
          )}
          <span>{t('followUpDate')}: {taskItem.followUpAt ? formatFullDate(taskItem.followUpAt) : t('noFollowUp')}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="task-header-editor">
      <p className="detail-kicker">{t('activeTask')}</p>
      <div className="task-title-row task-title-display-row">
        {editingField === 'title' ? (
          <input
            aria-label={t('editTask')}
            className="task-title-input"
            disabled={isSubmitting}
            onBlur={() => void saveChanges({}, { field: 'title' })}
            onChange={(event) => setTitle(event.target.value)}
            onKeyDown={handleTextKeyDown}
            required
            type="text"
            value={title}
          />
        ) : (
          <button
            className="heading-edit-trigger task-heading-trigger"
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('title');
            }}
            title={t('editTask')}
            type="button"
          >
            <h2>{taskItem.title}</h2>
          </button>
        )}
        <button
          className="icon-button header-edit-button"
          onClick={(event) => {
            event.stopPropagation();
            setEditingField('title');
          }}
          title={t('editTask')}
          type="button"
        >
          <Icon name="edit" />
        </button>
      </div>
      <div className="task-header-fields">
        <span>{t('created')}: {formatFullDate(taskItem.createdAt)}</span>
        <span title={`${t('lastUpdated')}: ${formatRelativeDate(taskItem.lastTouchedAt)}`}>
          {t('lastUpdated')}: {formatRelativeDate(taskItem.lastTouchedAt)}
        </span>
        {editingField === 'status' ? (
          <select
            aria-label={t('status')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges({}, { field: 'status' })}
            onChange={(event) => {
              setStatus(event.target.value);
              void saveChanges({ status: event.target.value }, { field: 'status' });
            }}
            value={status}
          >
            <option value="">{t('noStatus')}</option>
            {statusOptions.map((statusOption) => (
              <option key={statusOption} value={statusOption}>
                {statusOption}
              </option>
            ))}
          </select>
        ) : (
          <button
            className="task-meta-chip"
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('status');
            }}
            type="button"
          >
            {t('status')}: {taskItem.status ?? t('noStatus')}
          </button>
        )}
        {editingField === 'category' ? (
          <CategoryMultiSelect
            disabled={isSubmitting}
            onCancel={() => setEditingField(null)}
            onCommit={(nextCategories) => {
              const nextCategory = joinTaskCategories(nextCategories) ?? '';
              const nextProjectId = getPrimaryProjectIdForCategories(nextCategory, projects);
              setCategory(nextCategory);
              setCategoryProjectId(nextProjectId ?? '');
              void saveChanges(
                {
                  category: nextCategory,
                  projectId: nextProjectId,
                },
                { field: 'category' },
              );
            }}
            projects={projects}
            selectedCategories={selectedCategoryNames}
            t={t}
          />
        ) : (
          <button
            className="task-meta-chip"
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('category');
            }}
            style={getContextChipStyle(displayedProject?.color ?? null)}
            type="button"
          >
            {t('category')}: {displayedCategoryLabel}
          </button>
        )}
        {editingField === 'followUp' ? (
          <input
            aria-label={t('followUpDate')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges({}, { field: 'followUp' })}
            onChange={(event) => {
              setFollowUpDate(event.target.value);
              void saveChanges({ followUpDate: event.target.value }, { field: 'followUp' });
            }}
            type="date"
            value={followUpDate}
          />
        ) : (
          <button
            className="task-meta-chip follow-up-chip"
            data-tone={getFollowUpTone(taskItem.followUpAt)}
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('followUp');
            }}
            type="button"
          >
            {t('followUpDate')}: {taskItem.followUpAt ? formatFullDate(taskItem.followUpAt) : t('noFollowUp')}
          </button>
        )}
        {saveState !== 'idle' ? (
          <span
            aria-label={saveState === 'error' ? t('saveFailed') : t('saved')}
            className="saving-copy"
            data-state={saveState}
            role="status"
            title={saveState === 'error' ? t('saveFailed') : t('saved')}
          />
        ) : null}
      </div>
    </div>
  );
}
