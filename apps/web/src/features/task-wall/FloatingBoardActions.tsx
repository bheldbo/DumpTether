import { useEffect, useRef, useState } from 'react';
import { ColorOptionPicker } from '../../components/ColorOptionPicker';
import { Icon } from '../../components/Icon';
import { formatWorkspaceName } from '../../appUtils';
import { type Translate } from '../../localization';
import type {
  ProjectResponse,
  UpdateTaskItemRequest,
  WorkspaceResponse,
} from '../../types';

interface FloatingBoardActionsProps {
  archiveModeIsActive: boolean;
  canCreateTask: boolean;
  canManageSharing: boolean;
  canPermanentlyDelete: boolean;
  colorOptions: string[];
  editModeIsEnabled: boolean;
  onBatchUpdate: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onCopyTaskItemsToWorkspace: (workspaceId: string) => Promise<void>;
  onOpenCreateTask: () => void;
  onOpenBatchArchive: () => void;
  onOpenBatchReopen: () => void;
  onOpenBatchPermanentDelete: () => void;
  onOpenBatchShare: () => void;
  onToggleEditMode: () => void;
  projects: ProjectResponse[];
  selectedTaskCount: number;
  statusOptions: string[];
  taskCount: number;
  t: Translate;
  workspaces: WorkspaceResponse[];
}

export function FloatingBoardActions({
  archiveModeIsActive,
  canCreateTask,
  canManageSharing,
  canPermanentlyDelete,
  colorOptions,
  editModeIsEnabled,
  onBatchUpdate,
  onCopyTaskItemsToWorkspace,
  onOpenCreateTask,
  onOpenBatchArchive,
  onOpenBatchReopen,
  onOpenBatchPermanentDelete,
  onOpenBatchShare,
  onToggleEditMode,
  projects,
  selectedTaskCount,
  statusOptions,
  taskCount,
  t,
  workspaces,
}: FloatingBoardActionsProps) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const openActions = () => {
      setIsOpen(true);
    };

    window.addEventListener('dumptether:open-actions', openActions);

    return () => window.removeEventListener('dumptether:open-actions', openActions);
  }, []);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        menuRef.current &&
        event.target instanceof Node &&
        !menuRef.current.contains(event.target) &&
        !editModeIsEnabled
      ) {
        setIsOpen(false);
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);

    return () => window.removeEventListener('pointerdown', handlePointerDown);
  }, [editModeIsEnabled, isOpen]);

  const closeSelectionMode = () => {
    setIsOpen(false);
    onToggleEditMode();
  };

  return (
    <>
      {editModeIsEnabled ? (
        <button
          className="floating-selection-cancel"
          onClick={closeSelectionMode}
          title={t('cancel')}
          type="button"
        >
          <Icon name="close" />
          <span className="sr-only">{t('cancel')}</span>
        </button>
      ) : null}
      <div className="floating-board-actions" data-edit-mode={editModeIsEnabled} ref={menuRef}>
        <button
          className="quick-create-fab"
          data-active={isOpen}
          onClick={() => setIsOpen((open) => !open)}
          title={editModeIsEnabled
            ? `${selectedTaskCount} ${t('selectedTasks')}`
            : archiveModeIsActive ? t('archiveActions') : t('newTask')}
          type="button"
        >
          <Icon name={editModeIsEnabled ? 'check' : 'plus'} />
          <span>{editModeIsEnabled
            ? `${selectedTaskCount} ${t('selectedTasks')}`
            : archiveModeIsActive ? t('archiveActions') : t('newTask')}</span>
        </button>

        {isOpen ? (
          <div className="quick-action-menu">
            {canCreateTask ? (
              <button
                onClick={() => {
                  setIsOpen(false);
                  onOpenCreateTask();
                }}
                type="button"
              >
                <Icon name="plus" />
                <span>{t('addTask')}</span>
                <kbd>Alt+N</kbd>
              </button>
            ) : null}
            {editModeIsEnabled ? (
              <>
                <span className="quick-action-menu-label">
                  {selectedTaskCount} {t('selectedTasks')}
                </span>
                {archiveModeIsActive ? (
                  <>
                    <button
                      disabled={selectedTaskCount === 0}
                      onClick={() => {
                        onOpenBatchReopen();
                        setIsOpen(false);
                      }}
                      type="button"
                    >
                      <Icon name="undo" />
                      <span>{t('unarchiveSelected')}</span>
                    </button>
                    {canPermanentlyDelete ? (
                      <button
                        className="danger-action"
                        disabled={selectedTaskCount === 0}
                        onClick={() => {
                          onOpenBatchPermanentDelete();
                          setIsOpen(false);
                        }}
                        type="button"
                      >
                        <Icon name="trash" />
                        <span>{t('deletePermanently')}</span>
                      </button>
                    ) : null}
                  </>
                ) : (
                  <button
                    disabled={selectedTaskCount === 0}
                    onClick={() => {
                      onOpenBatchArchive();
                      setIsOpen(false);
                    }}
                    type="button"
                  >
                    <Icon name="archive" />
                    <span>{t('archiveSelected')}</span>
                  </button>
                )}
                {canManageSharing && !archiveModeIsActive ? (
                  <button
                    disabled={selectedTaskCount === 0}
                    onClick={() => {
                      onOpenBatchShare();
                      setIsOpen(false);
                    }}
                    type="button"
                  >
                    <Icon name="users" />
                    <span>{t('shareSelected')}</span>
                  </button>
                ) : null}
                <div className="batch-action-grid" aria-label={`${selectedTaskCount} ${t('selectedTasks')}`}>
                  <select
                    aria-label={t('copyToBoard')}
                    disabled={selectedTaskCount === 0 || workspaces.length === 0}
                    onChange={(event) => {
                      if (event.target.value) {
                        void onCopyTaskItemsToWorkspace(event.target.value);
                        setIsOpen(false);
                      }
                    }}
                    value=""
                  >
                    <option value="">{t('copyToBoard')}</option>
                    {workspaces.map((workspace) => (
                      <option key={workspace.id} value={workspace.id}>
                        {formatWorkspaceName(workspace.name, t)}
                      </option>
                    ))}
                  </select>
                  {!archiveModeIsActive ? (
                    <>
                      <select
                        aria-label={t('changeStatus')}
                        disabled={selectedTaskCount === 0}
                        onChange={(event) => {
                          if (event.target.value) {
                            void onBatchUpdate({ status: event.target.value });
                            setIsOpen(false);
                          }
                        }}
                        value=""
                      >
                        <option value="">{t('changeStatus')}</option>
                        {statusOptions.map((status) => (
                          <option key={status} value={status}>
                            {status}
                          </option>
                        ))}
                      </select>
                      <select
                        aria-label={t('changeCategory')}
                        disabled={selectedTaskCount === 0}
                        onChange={(event) => {
                          const project = projects.find((candidate) => candidate.id === event.target.value);
                          if (project) {
                            void onBatchUpdate({
                              projectId: project.id,
                              category: project.name,
                            });
                            setIsOpen(false);
                          }
                        }}
                        value=""
                      >
                        <option value="">{t('changeCategory')}</option>
                        {projects.map((project) => (
                          <option key={project.id} value={project.id}>
                            {project.name}
                          </option>
                        ))}
                      </select>
                      <ColorOptionPicker
                        emptyLabel={t('noTaskColors')}
                        label={t('changeColor')}
                        onChange={(color) => {
                          void onBatchUpdate({ color: color || null });
                          setIsOpen(false);
                        }}
                        options={colorOptions}
                        value=""
                        zeroLabel={t('changeColor')}
                      />
                      <input
                        aria-label={t('changeDueDate')}
                        disabled={selectedTaskCount === 0}
                        onChange={(event) => {
                          const followUpAt = event.target.value
                            ? new Date(`${event.target.value}T12:00:00`).toISOString()
                            : null;
                          void onBatchUpdate({ followUpAt });
                          setIsOpen(false);
                        }}
                        type="date"
                      />
                    </>
                  ) : null}
                </div>
                <button
                  className="ghost-button"
                  onClick={closeSelectionMode}
                  type="button"
                >
                  <Icon name="check" />
                  <span>{t('done')}</span>
                </button>
              </>
            ) : (
              taskCount > 0 ? (
                <button
                  onClick={() => {
                    onToggleEditMode();
                    setIsOpen(false);
                  }}
                  type="button"
                >
                  <Icon name="check" />
                  <span>{t('selectTasksForAction')}</span>
                  <kbd>Alt+X</kbd>
                </button>
              ) : null
            )}
          </div>
        ) : null}
      </div>
    </>
  );
}
