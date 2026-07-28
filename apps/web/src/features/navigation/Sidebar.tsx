import {
  type FormEvent,
  type MouseEvent,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Icon } from '../../components/Icon';
import { DeleteWorkspaceDialog } from '../../components/DeleteWorkspaceDialog';
import type {
  ConnectionStatus,
  WorkspaceMode,
} from '../../appTypes';
import {
  formatDateTime,
  formatRelativeDate,
  formatSavedViewName,
  formatWorkspaceName,
  getViewIcon,
  isOwnerRole,
  isSystemAllTasksWorkspace,
  isTaskShareWorkspace,
} from '../../appUtils';
import type { Language, Translate } from '../../localization';
import { getSidebarStyle } from '../../taskUtils';
import type {
  CurrentUserResponse,
  SavedViewResponse,
  UpdateWorkspaceRequest,
  WorkspaceResponse,
} from '../../types';

export function Sidebar({
  accountNotificationCount,
  connectionStatus,
  counts,
  currentUser,
  currentViewId,
  lastPingedAt,
  language,
  localDesktopSessionIsActive,
  mode,
  onCreateWorkspace,
  onDeleteWorkspace,
  onLeaveWorkspaceAccess,
  onOpenAccount,
  onOpenSettings,
  onOpenTemplates,
  onRefresh,
  onResizeStart,
  onSelectWorkspace,
  onSelectView,
  onToggleSidebar,
  onUpdateWorkspace,
  savedViews,
  sidebarIsCollapsed,
  t,
  temporarySessionIsActive,
  templateCount,
  workspace,
  workspaces,
}: {
  accountNotificationCount: number;
  connectionStatus: ConnectionStatus;
  counts: Record<string, number>;
  currentUser: CurrentUserResponse | null;
  currentViewId: string | null;
  lastPingedAt: string | null;
  language: Language;
  localDesktopSessionIsActive: boolean;
  mode: WorkspaceMode;
  onCreateWorkspace: (name: string) => Promise<void>;
  onDeleteWorkspace: (workspaceId: string) => Promise<void>;
  onLeaveWorkspaceAccess: (workspaceId: string) => Promise<void>;
  onOpenAccount: () => void;
  onOpenSettings: () => void;
  onOpenTemplates: () => void;
  onRefresh: () => void;
  onResizeStart: (event: MouseEvent<HTMLButtonElement>) => void;
  onSelectWorkspace: (workspaceId: string) => void;
  onSelectView: (viewId: string) => void;
  onToggleSidebar: () => void;
  onUpdateWorkspace: (
    workspaceId: string,
    requestBody: UpdateWorkspaceRequest,
  ) => Promise<void>;
  savedViews: SavedViewResponse[];
  sidebarIsCollapsed: boolean;
  t: Translate;
  temporarySessionIsActive: boolean;
  templateCount: number;
  workspace: WorkspaceResponse | null;
  workspaces: WorkspaceResponse[];
}) {
  const [workspaceDraft, setWorkspaceDraft] = useState('');
  const [workspaceCreateIsOpen, setWorkspaceCreateIsOpen] = useState(false);
  const [editingWorkspaceId, setEditingWorkspaceId] = useState<string | null>(null);
  const [editingWorkspaceName, setEditingWorkspaceName] = useState('');
  const [pendingDeleteWorkspace, setPendingDeleteWorkspace] =
    useState<WorkspaceResponse | null>(null);
  const [pendingWorkspaceLeaveId, setPendingWorkspaceLeaveId] = useState<string | null>(null);
  const [workspaceIsSubmitting, setWorkspaceIsSubmitting] = useState(false);
  const workspaceCreateFormRef = useRef<HTMLFormElement>(null);
  const workspaceInputRef = useRef<HTMLInputElement>(null);
  const workspaceCreateToggleRef = useRef<HTMLButtonElement>(null);
  const workspaceMembershipsById = useMemo(
    () => new Map(currentUser?.workspaces.map((workspaceItem) => [workspaceItem.id, workspaceItem]) ?? []),
    [currentUser],
  );
  const visibleSavedViews = useMemo(
    () => savedViews.filter((view) => ['all tasks', 'overview', 'archive'].includes(view.name.toLowerCase())),
    [savedViews],
  );

  useEffect(() => {
    if (workspaceCreateIsOpen) {
      workspaceInputRef.current?.focus();
    }
  }, [workspaceCreateIsOpen]);

  useEffect(() => {
    if (!workspaceCreateIsOpen) {
      return undefined;
    }

    const closeWorkspaceCreate = () => {
      setWorkspaceCreateIsOpen(false);
      setWorkspaceDraft('');
    };
    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target;

      if (!(target instanceof Node)) {
        return;
      }

      if (
        workspaceCreateFormRef.current?.contains(target) ||
        workspaceCreateToggleRef.current?.contains(target)
      ) {
        return;
      }

      closeWorkspaceCreate();
    };
    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeWorkspaceCreate();
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);
    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('pointerdown', handlePointerDown);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [workspaceCreateIsOpen]);

  const displayedConnectionState = localDesktopSessionIsActive ? 'local' : connectionStatus;
  const displayedConnectionLabel = localDesktopSessionIsActive
    ? t('localDesktopModeShort')
    : connectionStatus === 'online'
      ? t('online')
      : t('offline');
  const connectionTitle = `${
    localDesktopSessionIsActive
      ? t('localDesktopModePersistent')
      : connectionStatus === 'online'
        ? t('backendOnline')
        : t('backendOffline')
  }${lastPingedAt ? ` - ${t('lastPinged')}: ${formatDateTime(lastPingedAt)}` : ''}`;

  const handleBrandClick = (event: MouseEvent<HTMLDivElement>) => {
    if (!window.matchMedia('(max-width: 1100px)').matches) {
      return;
    }

    if (
      event.target instanceof HTMLElement &&
      event.target.closest('button, a, input, select, textarea')
    ) {
      return;
    }

    onToggleSidebar();
  };

  const submitWorkspace = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = workspaceDraft.trim();

    if (!trimmedName) {
      return;
    }

    setWorkspaceIsSubmitting(true);
    try {
      await onCreateWorkspace(trimmedName);
      setWorkspaceDraft('');
      setWorkspaceCreateIsOpen(false);
    } finally {
      setWorkspaceIsSubmitting(false);
    }
  };

  const startWorkspaceEdit = (workspaceItem: WorkspaceResponse) => {
    setEditingWorkspaceId(workspaceItem.id);
    setEditingWorkspaceName(workspaceItem.name);
    setPendingWorkspaceLeaveId(null);
  };

  const cancelWorkspaceEdit = () => {
    setEditingWorkspaceId(null);
    setEditingWorkspaceName('');
  };

  const submitWorkspaceEdit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = editingWorkspaceName.trim();
    if (!editingWorkspaceId || !trimmedName) {
      return;
    }

    await onUpdateWorkspace(editingWorkspaceId, { name: trimmedName });
    cancelWorkspaceEdit();
  };

  return (
    <>
      <aside
        className="sidebar"
        aria-label="DumpTether navigation"
        style={getSidebarStyle(workspace?.color ?? null)}
      >
        <div className="brand" onClick={handleBrandClick}>
          <button
            className="brand-mark"
            onClick={sidebarIsCollapsed ? onToggleSidebar : undefined}
            title={sidebarIsCollapsed ? t('expandSidebar') : undefined}
            type="button"
          >
            DT
          </button>
          <div className="brand-copy">
            <p className="brand-name">DumpTether</p>
            <p className="brand-subtitle">Personal task evidence</p>
          </div>
          <span
            className="mobile-connection-dot"
            data-state={displayedConnectionState}
            title={connectionTitle}
          />
          <button
            className="icon-button sidebar-toggle"
            onClick={onToggleSidebar}
            title={sidebarIsCollapsed ? t('expandSidebar') : t('collapseSidebar')}
            type="button"
          >
            <Icon name="panel" />
          </button>
        </div>
        {!sidebarIsCollapsed ? (
          <button
            aria-label="Resize sidebar"
            className="sidebar-resizer"
            onMouseDown={onResizeStart}
            type="button"
          />
        ) : null}

        <div className="sidebar-section-label">
          <span>{t('workspaces')}</span>
          <button
            className="tiny-icon-button"
            onClick={() => setWorkspaceCreateIsOpen((isOpen) => !isOpen)}
            ref={workspaceCreateToggleRef}
            title={t('newWorkspace')}
            type="button"
          >
            <Icon name="plus" />
          </button>
        </div>

        <nav className="view-nav workspace-nav" aria-label={t('workspaces')}>
          {workspaces.map((candidate) => {
            const isSharedOnly = isTaskShareWorkspace(candidate);
            const isSystemBoard = isSystemAllTasksWorkspace(candidate);
            const membership = workspaceMembershipsById.get(candidate.id);
            const isOwner = Boolean(membership && isOwnerRole(membership.role));
            const isSharedMembership = Boolean(membership && !isOwnerRole(membership.role));
            const canDelete = Boolean(membership && isOwnerRole(membership.role) && !isSharedOnly && !isSystemBoard);
            const canEdit = canDelete;
            const canLeave = isSharedOnly || isSharedMembership;
            const isEditing = editingWorkspaceId === candidate.id;
            const leaveIsPending = pendingWorkspaceLeaveId === candidate.id;
            const isSharedAccess = isSharedOnly || isSharedMembership;
            const ownerSharedSignalIsVisible = isOwner &&
              !isSharedAccess &&
              ((candidate.memberCount ?? 1) > 1 ||
                (candidate.pendingInvitationCount ?? 0) > 0);

            return (
              <div
                className="workspace-nav-row"
                key={candidate.id}
              >
                {isEditing ? (
                  <form className="workspace-row-editor" onSubmit={(event) => void submitWorkspaceEdit(event)}>
                    <span
                      className="workspace-color-dot"
                      style={{ backgroundColor: candidate.color ?? '#184c48' }}
                    />
                    <input
                      aria-label={t('editBoard')}
                      autoFocus
                      onChange={(event) => setEditingWorkspaceName(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === 'Escape') {
                          cancelWorkspaceEdit();
                        }
                      }}
                      type="text"
                      value={editingWorkspaceName}
                    />
                    <button
                      className="tiny-icon-button"
                      disabled={!editingWorkspaceName.trim()}
                      title={t('saved')}
                      type="submit"
                    >
                      <Icon name="check" />
                    </button>
                    <button
                      className="tiny-icon-button"
                      onClick={cancelWorkspaceEdit}
                      title={t('cancel')}
                      type="button"
                    >
                      <Icon name="close" />
                    </button>
                    {canDelete ? (
                      <button
                        className="tiny-icon-button danger-icon-button"
                        onClick={() => setPendingDeleteWorkspace(candidate)}
                        title={t('deleteBoard')}
                        type="button"
                      >
                        <Icon name="trash" />
                      </button>
                    ) : null}
                  </form>
                ) : (
                  <button
                    aria-current={workspace?.id === candidate.id ? 'page' : undefined}
                    className={`nav-item workspace-nav-item${isSharedAccess ? ' is-shared-access' : ''}`}
                    onClick={() => onSelectWorkspace(candidate.id)}
                    title={isSharedAccess
                      ? `${formatWorkspaceName(candidate.name, t)} - ${t('sharedWorkspace')}`
                      : formatWorkspaceName(candidate.name, t)}
                    type="button"
                  >
                    <span
                      className="workspace-color-dot"
                      style={{ backgroundColor: candidate.color ?? '#184c48' }}
                    />
                    <span className="nav-label">{formatWorkspaceName(candidate.name, t)}</span>
                    {ownerSharedSignalIsVisible ? (
                      <span className="owner-workspace-badge" title={t('roleOwner')}>
                        <Icon name="crown" />
                      </span>
                    ) : null}
                    {isSharedAccess ? (
                      <span className="shared-workspace-badge" title={t('sharedWorkspace')}>
                        <Icon name={isSharedOnly ? 'users' : 'user'} />
                        {isSharedOnly ? candidate.sharedTaskCount ?? 0 : null}
                      </span>
                    ) : null}
                  </button>
                )}
                <span className="workspace-row-actions">
                  {canEdit && !isEditing ? (
                    <button
                      className="tiny-icon-button workspace-row-action"
                      onClick={() => startWorkspaceEdit(candidate)}
                      title={t('editBoard')}
                      type="button"
                    >
                      <Icon name="edit" />
                    </button>
                  ) : null}
                  {canLeave ? (
                    leaveIsPending ? (
                      <span className="workspace-row-confirm">
                        <button
                          className="tiny-icon-button"
                          onClick={() => void onLeaveWorkspaceAccess(candidate.id)}
                          title={t('leaveBoard')}
                          type="button"
                        >
                          <Icon name="check" />
                        </button>
                        <button
                          className="tiny-icon-button"
                          onClick={() => setPendingWorkspaceLeaveId(null)}
                          title={t('cancel')}
                          type="button"
                        >
                          <Icon name="close" />
                        </button>
                      </span>
                    ) : (
                      <button
                        className="tiny-icon-button workspace-row-action"
                        onClick={() => setPendingWorkspaceLeaveId(candidate.id)}
                        title={t('leaveBoard')}
                        type="button"
                      >
                        <Icon name="logout" />
                      </button>
                    )
                  ) : null}
                </span>
              </div>
            );
          })}
          {workspaceCreateIsOpen ? (
            <form
              className="sidebar-inline-form"
              onSubmit={submitWorkspace}
              ref={workspaceCreateFormRef}
            >
              <input
                aria-label={t('newWorkspace')}
                onChange={(event) => setWorkspaceDraft(event.target.value)}
                placeholder={t('newWorkspace')}
                ref={workspaceInputRef}
                type="text"
                value={workspaceDraft}
              />
              <button
                className="icon-button"
                disabled={!workspaceDraft.trim() || workspaceIsSubmitting}
                title={t('newWorkspace')}
                type="submit"
              >
                <Icon name="check" />
              </button>
              <button
                className="icon-button"
                onClick={() => {
                  setWorkspaceDraft('');
                  setWorkspaceCreateIsOpen(false);
                }}
                title={t('cancel')}
                type="button"
              >
                <Icon name="close" />
              </button>
            </form>
          ) : null}
        </nav>

        <div className="sidebar-separator" />

        <div className="sidebar-section-label">
          <span>{t('savedViews')}</span>
        </div>

        <nav className="view-nav" aria-label={t('savedViews')}>
          {visibleSavedViews.map((view) => (
            <button
              aria-current={mode === 'tasks' && currentViewId === view.id ? 'page' : undefined}
              className="nav-item"
              key={view.id}
              onClick={() => onSelectView(view.id)}
              title={formatSavedViewName(view.name, t)}
              type="button"
            >
              <Icon name={getViewIcon(view)} />
              <span className="nav-label">{formatSavedViewName(view.name, t)}</span>
              <span className="nav-count">{counts[view.id] ?? 0}</span>
            </button>
          ))}
        </nav>

        <div className="sidebar-separator sidebar-separator-actions" />

        <div className="sidebar-actions">
          <button
            aria-current={mode === 'templates' ? 'page' : undefined}
            className="nav-item"
            onClick={onOpenTemplates}
            type="button"
          >
            <Icon name="templates" />
            <span className="nav-label">{t('templates')}</span>
            <span className="nav-count">{templateCount}</span>
          </button>
          <button className="nav-item" onClick={onOpenSettings} type="button">
            <Icon name="settings" />
            <span className="nav-label">{t('settings')}</span>
            <span className="nav-count">{language.toUpperCase()}</span>
          </button>
          <button className="nav-item" onClick={onOpenAccount} type="button">
            <Icon name="user" />
            <span className="nav-label">{t('account')}</span>
            {accountNotificationCount > 0 ? (
              <span className="nav-count">{accountNotificationCount}</span>
            ) : temporarySessionIsActive ? (
              <span className="nav-count">{t('guestModeShort')}</span>
            ) : localDesktopSessionIsActive ? (
              <span className="nav-count">{t('localDesktopModeShort')}</span>
            ) : null}
          </button>
          <button className="refresh-button" onClick={onRefresh} type="button">
            <Icon name="refresh" />
            <span className="nav-label">{t('refresh')}</span>
          </button>
          {temporarySessionIsActive ? (
            <button className="nav-item guest-warning-link" onClick={onOpenAccount} type="button">
              <Icon name="waiting" />
              <span className="nav-label">{t('guestModeShort')}</span>
            </button>
          ) : null}
          <div className="sidebar-footer">
            <span
              className="connection-indicator"
              data-state={displayedConnectionState}
              title={connectionTitle}
            >
              <span />
              <strong>{displayedConnectionLabel}</strong>
              {lastPingedAt ? (
                <small>{formatRelativeDate(lastPingedAt)}</small>
              ) : null}
            </span>
            <a href="https://github.com/bheldbo/DumpTether" rel="noreferrer" target="_blank">
              GitHub
            </a>
            <span>(c) 2026</span>
          </div>
        </div>
      </aside>
      {pendingDeleteWorkspace ? (
        <DeleteWorkspaceDialog
          onClose={() => setPendingDeleteWorkspace(null)}
          onDelete={async () => {
            await onDeleteWorkspace(pendingDeleteWorkspace.id);
            setPendingDeleteWorkspace(null);
          }}
          t={t}
          workspace={pendingDeleteWorkspace}
        />
      ) : null}
    </>
  );
}
