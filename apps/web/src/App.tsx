import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import {
  addTaskTimelineEntry,
  archiveTaskItem,
  createTaskItem,
  getTaskItem,
  listArchiveResolutions,
  listTaskItems,
  reopenTaskItem,
} from './api';
import './App.css';
import { FieldValueList } from './fieldRenderers';
import type {
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  TaskItemDetailResponse,
  TaskItemSummaryResponse,
} from './types';

type ViewId = 'inbox' | 'active' | 'waiting' | 'stale' | 'archive';

interface ViewDefinition {
  id: ViewId;
  label: string;
  icon: IconName;
}

type IconName =
  | 'archive'
  | 'clock'
  | 'inbox'
  | 'list'
  | 'note'
  | 'plus'
  | 'refresh'
  | 'waiting';

const viewDefinitions: ViewDefinition[] = [
  { id: 'inbox', label: 'Inbox', icon: 'inbox' },
  { id: 'active', label: 'All active', icon: 'list' },
  { id: 'waiting', label: 'Waiting', icon: 'waiting' },
  { id: 'stale', label: 'Not touched', icon: 'clock' },
  { id: 'archive', label: 'Archive', icon: 'archive' },
];

const staleAfterDays = 7;

function App() {
  const [activeTaskItems, setActiveTaskItems] = useState<TaskItemSummaryResponse[]>([]);
  const [archivedTaskItems, setArchivedTaskItems] = useState<TaskItemSummaryResponse[]>([]);
  const [archiveResolutions, setArchiveResolutions] = useState<ArchiveResolutionResponse[]>([]);
  const [currentView, setCurrentView] = useState<ViewId>('inbox');
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [selectedTask, setSelectedTask] = useState<TaskItemDetailResponse | null>(null);
  const [isArchiveDialogOpen, setIsArchiveDialogOpen] = useState(false);
  const [isLoadingWorkspace, setIsLoadingWorkspace] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadWorkspace = useCallback(async () => {
    setIsLoadingWorkspace(true);

    try {
      const [active, archive, resolutions] = await Promise.all([
        listTaskItems('Active'),
        listTaskItems('Archive'),
        listArchiveResolutions(),
      ]);

      setActiveTaskItems(active);
      setArchivedTaskItems(archive);
      setArchiveResolutions(resolutions);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsLoadingWorkspace(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const visibleTaskItems = useMemo(() => {
    return getVisibleTaskItems(currentView, activeTaskItems, archivedTaskItems);
  }, [activeTaskItems, archivedTaskItems, currentView]);

  const counts = useMemo(() => {
    return {
      inbox: getVisibleTaskItems('inbox', activeTaskItems, archivedTaskItems).length,
      active: activeTaskItems.length,
      waiting: getVisibleTaskItems('waiting', activeTaskItems, archivedTaskItems).length,
      stale: getVisibleTaskItems('stale', activeTaskItems, archivedTaskItems).length,
      archive: archivedTaskItems.length,
    } satisfies Record<ViewId, number>;
  }, [activeTaskItems, archivedTaskItems]);

  useEffect(() => {
    if (visibleTaskItems.length === 0) {
      setSelectedTaskId(null);
      return;
    }

    const selectedTaskIsVisible = visibleTaskItems.some(
      (taskItem) => taskItem.id === selectedTaskId,
    );

    if (!selectedTaskIsVisible) {
      setSelectedTaskId(visibleTaskItems[0].id);
    }
  }, [selectedTaskId, visibleTaskItems]);

  useEffect(() => {
    if (!selectedTaskId) {
      setSelectedTask(null);
      return;
    }

    let isStaleRequest = false;
    setIsLoadingDetail(true);

    getTaskItem(selectedTaskId)
      .then((taskItem) => {
        if (!isStaleRequest) {
          setSelectedTask(taskItem);
          setErrorMessage(null);
        }
      })
      .catch((error) => {
        if (!isStaleRequest) {
          setErrorMessage(getErrorMessage(error));
        }
      })
      .finally(() => {
        if (!isStaleRequest) {
          setIsLoadingDetail(false);
        }
      });

    return () => {
      isStaleRequest = true;
    };
  }, [selectedTaskId]);

  const handleCreateTaskItem = async (title: string) => {
    try {
      const created = await createTaskItem({ title });
      setCurrentView('inbox');
      setSelectedTaskId(created.id);
      setSelectedTask(created);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleAddTimelineEntry = async (note: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await addTaskTimelineEntry(selectedTask.id, { note });
      setSelectedTask(updated);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleArchiveTaskItem = async (requestBody: ArchiveTaskItemRequest) => {
    if (!selectedTask) {
      return;
    }

    try {
      const archived = await archiveTaskItem(selectedTask.id, requestBody);
      setCurrentView('archive');
      setSelectedTaskId(archived.id);
      setSelectedTask(archived);
      setIsArchiveDialogOpen(false);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleReopenTaskItem = async (note?: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const reopened = await reopenTaskItem(selectedTask.id, { note });
      setCurrentView('active');
      setSelectedTaskId(reopened.id);
      setSelectedTask(reopened);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  return (
    <main className="app-shell">
      <Sidebar
        counts={counts}
        currentView={currentView}
        onRefresh={loadWorkspace}
        onSelectView={setCurrentView}
      />

      <section className="workspace" aria-label="Task workspace">
        {errorMessage ? (
          <div className="error-banner" role="alert">
            <strong>Something needs attention.</strong>
            <span>{errorMessage}</span>
          </div>
        ) : null}

        <TaskList
          currentView={currentView}
          isLoading={isLoadingWorkspace}
          onCreateTaskItem={handleCreateTaskItem}
          onSelectTaskItem={setSelectedTaskId}
          selectedTaskId={selectedTaskId}
          taskItems={visibleTaskItems}
        />

        <TaskDetail
          archiveResolutions={archiveResolutions}
          isArchiveDialogOpen={isArchiveDialogOpen}
          isLoading={isLoadingDetail}
          onAddTimelineEntry={handleAddTimelineEntry}
          onArchive={handleArchiveTaskItem}
          onCloseArchiveDialog={() => setIsArchiveDialogOpen(false)}
          onOpenArchiveDialog={() => setIsArchiveDialogOpen(true)}
          onReopen={handleReopenTaskItem}
          taskItem={selectedTask}
        />
      </section>
    </main>
  );
}

function Sidebar({
  counts,
  currentView,
  onRefresh,
  onSelectView,
}: {
  counts: Record<ViewId, number>;
  currentView: ViewId;
  onRefresh: () => void;
  onSelectView: (viewId: ViewId) => void;
}) {
  return (
    <aside className="sidebar" aria-label="DumpTether navigation">
      <div className="brand">
        <div className="brand-mark">DT</div>
        <div>
          <p className="brand-name">DumpTether</p>
          <p className="brand-subtitle">Personal task evidence</p>
        </div>
      </div>

      <nav className="view-nav" aria-label="Task views">
        {viewDefinitions.map((view) => (
          <button
            aria-current={currentView === view.id ? 'page' : undefined}
            className="nav-item"
            key={view.id}
            onClick={() => onSelectView(view.id)}
            type="button"
          >
            <Icon name={view.icon} />
            <span>{view.label}</span>
            <span className="nav-count">{counts[view.id]}</span>
          </button>
        ))}
      </nav>

      <button className="refresh-button" onClick={onRefresh} type="button">
        <Icon name="refresh" />
        <span>Refresh</span>
      </button>
    </aside>
  );
}

function TaskList({
  currentView,
  isLoading,
  onCreateTaskItem,
  onSelectTaskItem,
  selectedTaskId,
  taskItems,
}: {
  currentView: ViewId;
  isLoading: boolean;
  onCreateTaskItem: (title: string) => Promise<void>;
  onSelectTaskItem: (id: string) => void;
  selectedTaskId: string | null;
  taskItems: TaskItemSummaryResponse[];
}) {
  return (
    <section className="task-list" aria-labelledby="task-list-title">
      <div className="list-header">
        <div>
          <h1 id="task-list-title">{getViewLabel(currentView)}</h1>
          <p>{getViewDescription(currentView)}</p>
        </div>
      </div>

      {currentView !== 'archive' ? (
        <CreateTaskForm onCreateTaskItem={onCreateTaskItem} />
      ) : null}

      <div className="list-body" aria-busy={isLoading}>
        {isLoading ? <p className="empty-copy">Loading tasks...</p> : null}

        {!isLoading && taskItems.length === 0 ? (
          <p className="empty-copy">Nothing in this view yet.</p>
        ) : null}

        {taskItems.map((taskItem) => (
          <button
            className="task-row"
            data-selected={selectedTaskId === taskItem.id}
            key={taskItem.id}
            onClick={() => onSelectTaskItem(taskItem.id)}
            type="button"
          >
            <span className="task-row-title">{taskItem.title}</span>
            <span className="task-row-meta">
              {taskItem.status ?? 'No status'} · touched{' '}
              {formatRelativeDate(taskItem.lastTouchedAt)}
            </span>
            {taskItem.followUpAt ? (
              <span className="task-row-follow-up">
                Follow up {formatShortDate(taskItem.followUpAt)}
              </span>
            ) : null}
          </button>
        ))}
      </div>
    </section>
  );
}

function CreateTaskForm({
  onCreateTaskItem,
}: {
  onCreateTaskItem: (title: string) => Promise<void>;
}) {
  const [title, setTitle] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return;
    }

    setIsSubmitting(true);
    await onCreateTaskItem(trimmedTitle);
    setTitle('');
    setIsSubmitting(false);
  };

  return (
    <form className="create-task-form" onSubmit={handleSubmit}>
      <input
        aria-label="New task title"
        onChange={(event) => setTitle(event.target.value)}
        placeholder="Capture a task..."
        type="text"
        value={title}
      />
      <button disabled={!title.trim() || isSubmitting} type="submit">
        <Icon name="plus" />
        <span>Add</span>
      </button>
    </form>
  );
}

function TaskDetail({
  archiveResolutions,
  isArchiveDialogOpen,
  isLoading,
  onAddTimelineEntry,
  onArchive,
  onCloseArchiveDialog,
  onOpenArchiveDialog,
  onReopen,
  taskItem,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  isArchiveDialogOpen: boolean;
  isLoading: boolean;
  onAddTimelineEntry: (note: string) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onCloseArchiveDialog: () => void;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  taskItem: TaskItemDetailResponse | null;
}) {
  const [reopenNote, setReopenNote] = useState('');

  useEffect(() => {
    setReopenNote('');
  }, [taskItem?.id]);

  if (!taskItem) {
    return (
      <section className="task-detail empty-detail" aria-label="Task detail">
        <p>Select a task to see its structured fields and timeline.</p>
      </section>
    );
  }

  return (
    <section className="task-detail" aria-busy={isLoading} aria-label="Task detail">
      <div className="detail-header">
        <div>
          <p className="detail-kicker">
            {taskItem.archivedAt ? 'Archived task' : 'Active task'}
          </p>
          <h2>{taskItem.title}</h2>
        </div>

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
            <span>Archive</span>
          </button>
        )}
      </div>

      <div className="detail-meta">
        <MetaItem label="Status" value={taskItem.status ?? 'No status'} />
        <MetaItem label="Created" value={formatDateTime(taskItem.createdAt)} />
        <MetaItem label="Touched" value={formatDateTime(taskItem.lastTouchedAt)} />
        <MetaItem
          label="Follow-up"
          value={taskItem.followUpAt ? formatDateTime(taskItem.followUpAt) : 'None'}
        />
      </div>

      <section className="detail-section" aria-labelledby="fields-title">
        <h3 id="fields-title">Structured fields</h3>
        <FieldValueList fieldValues={taskItem.fieldValues} />
      </section>

      <TimelinePanel
        onAddTimelineEntry={onAddTimelineEntry}
        timelineEntries={taskItem.timelineEntries}
      />

      {isArchiveDialogOpen ? (
        <ArchiveDialog
          archiveResolutions={archiveResolutions}
          onArchive={onArchive}
          onClose={onCloseArchiveDialog}
          taskTitle={taskItem.title}
        />
      ) : null}
    </section>
  );
}

function MetaItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="meta-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function TimelinePanel({
  onAddTimelineEntry,
  timelineEntries,
}: {
  onAddTimelineEntry: (note: string) => Promise<void>;
  timelineEntries: TaskItemDetailResponse['timelineEntries'];
}) {
  return (
    <section className="timeline-panel" aria-labelledby="timeline-title">
      <div className="section-heading">
        <h3 id="timeline-title">Timeline</h3>
        <span>{timelineEntries.length} entries</span>
      </div>

      <AddTimelineEntryForm onAddTimelineEntry={onAddTimelineEntry} />

      <ol className="timeline-list">
        {timelineEntries.map((entry) => (
          <li className="timeline-entry" key={entry.id}>
            <div className="timeline-dot" aria-hidden="true" />
            <div>
              <div className="timeline-entry-header">
                <strong>{entry.summary}</strong>
                <time dateTime={entry.occurredAt}>{formatDateTime(entry.occurredAt)}</time>
              </div>
              {entry.details ? <p>{entry.details}</p> : null}
              <span className="timeline-kind">{entry.kind}</span>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}

function AddTimelineEntryForm({
  onAddTimelineEntry,
}: {
  onAddTimelineEntry: (note: string) => Promise<void>;
}) {
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedNote = note.trim();
    if (!trimmedNote) {
      return;
    }

    setIsSubmitting(true);
    await onAddTimelineEntry(trimmedNote);
    setNote('');
    setIsSubmitting(false);
  };

  return (
    <form className="timeline-form" onSubmit={handleSubmit}>
      <textarea
        aria-label="Timeline note"
        onChange={(event) => setNote(event.target.value)}
        placeholder="Add evidence, a status note, or a source detail..."
        rows={3}
        value={note}
      />
      <button disabled={!note.trim() || isSubmitting} type="submit">
        <Icon name="note" />
        <span>Add note</span>
      </button>
    </form>
  );
}

function ArchiveDialog({
  archiveResolutions,
  onArchive,
  onClose,
  taskTitle,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onClose: () => void;
  taskTitle: string;
}) {
  const [archiveResolutionId, setArchiveResolutionId] = useState(
    archiveResolutions[0]?.id ?? '',
  );
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const selectedResolution = archiveResolutions.find(
    (resolution) => resolution.id === archiveResolutionId,
  );
  const noteIsRequired = selectedResolution?.requiresExplanation ?? false;
  const canSubmit = Boolean(archiveResolutionId) && (!noteIsRequired || note.trim());

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!canSubmit) {
      return;
    }

    setIsSubmitting(true);
    await onArchive({
      archiveResolutionId,
      note: note.trim() || null,
    });
    setIsSubmitting(false);
  };

  return (
    <div className="dialog-backdrop" role="presentation">
      <section
        aria-labelledby="archive-dialog-title"
        aria-modal="true"
        className="archive-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">Archive task</p>
            <h2 id="archive-dialog-title">{taskTitle}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <span aria-hidden="true">x</span>
            <span className="sr-only">Close archive dialog</span>
          </button>
        </div>

        <form className="archive-form" onSubmit={handleSubmit}>
          <label>
            Resolution reason
            <select
              onChange={(event) => setArchiveResolutionId(event.target.value)}
              required
              value={archiveResolutionId}
            >
              {archiveResolutions.length === 0 ? (
                <option value="">No archive reasons available</option>
              ) : null}
              {archiveResolutions.map((resolution) => (
                <option key={resolution.id} value={resolution.id}>
                  {resolution.name}
                </option>
              ))}
            </select>
          </label>

          {selectedResolution?.description ? (
            <p className="resolution-description">{selectedResolution.description}</p>
          ) : null}

          <label>
            Archive note {noteIsRequired ? '(required)' : '(optional)'}
            <textarea
              onChange={(event) => setNote(event.target.value)}
              required={noteIsRequired}
              rows={4}
              value={note}
            />
          </label>

          <div className="dialog-actions">
            <button className="ghost-button" onClick={onClose} type="button">
              Cancel
            </button>
            <button disabled={!canSubmit || isSubmitting} type="submit">
              Archive
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

function Icon({ name }: { name: IconName }) {
  const paths: Record<IconName, string> = {
    archive: 'M4 7h16v13H4V7Zm2-4h12l2 4H4l2-4Zm5 8h2',
    clock: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16Zm0 4v5l3 2',
    inbox: 'M4 5h16v10l-3 4H7l-3-4V5Zm0 10h5l1.5 2h3L15 15h5',
    list: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
    note: 'M5 4h11l3 3v13H5V4Zm11 0v4h4M8 12h8M8 16h6',
    plus: 'M12 5v14M5 12h14',
    refresh: 'M20 7v5h-5M4 17v-5h5M18 10a6 6 0 0 0-10-4L4 10m2 4a6 6 0 0 0 10 4l4-4',
    waiting: 'M6 4h12M8 4v5l4 3 4-3V4M8 20v-5l4-3 4 3v5M6 20h12',
  };

  return (
    <svg
      aria-hidden="true"
      className="icon"
      fill="none"
      focusable="false"
      viewBox="0 0 24 24"
    >
      <path d={paths[name]} stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" />
    </svg>
  );
}

function getVisibleTaskItems(
  viewId: ViewId,
  activeTaskItems: TaskItemSummaryResponse[],
  archivedTaskItems: TaskItemSummaryResponse[],
) {
  switch (viewId) {
    case 'archive':
      return archivedTaskItems;
    case 'waiting':
      return activeTaskItems.filter((taskItem) =>
        taskItem.status?.toLowerCase().includes('waiting'),
      );
    case 'stale':
      return activeTaskItems.filter((taskItem) => {
        const touchedAt = new Date(taskItem.lastTouchedAt).getTime();
        const staleAt = Date.now() - staleAfterDays * 24 * 60 * 60 * 1000;
        return touchedAt < staleAt;
      });
    case 'inbox':
      return activeTaskItems.filter((taskItem) => !taskItem.status);
    case 'active':
    default:
      return activeTaskItems;
  }
}

function getViewLabel(viewId: ViewId) {
  return viewDefinitions.find((view) => view.id === viewId)?.label ?? 'Tasks';
}

function getViewDescription(viewId: ViewId) {
  switch (viewId) {
    case 'archive':
      return 'Closed tasks stay visible with their evidence trail.';
    case 'waiting':
      return 'Active tasks currently marked as waiting.';
    case 'stale':
      return `Active tasks not touched in ${staleAfterDays} days.`;
    case 'active':
      return 'Every active task in the development workspace.';
    case 'inbox':
    default:
      return 'Fresh captures without a status yet.';
  }
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

function formatShortDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

function formatRelativeDate(value: string) {
  const elapsedMs = Date.now() - new Date(value).getTime();
  const elapsedMinutes = Math.max(1, Math.round(elapsedMs / 60000));

  if (elapsedMinutes < 60) {
    return `${elapsedMinutes}m ago`;
  }

  const elapsedHours = Math.round(elapsedMinutes / 60);
  if (elapsedHours < 24) {
    return `${elapsedHours}h ago`;
  }

  return `${Math.round(elapsedHours / 24)}d ago`;
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Unexpected error.';
}

export default App;
