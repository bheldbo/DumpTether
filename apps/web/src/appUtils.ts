import { type IconName } from './components/Icon';
import {
  languageStorageKey,
  workspaceStorageKey,
  type WorkspaceMode,
} from './appTypes';
import { type Language, type Translate } from './localization';
import { uniqueSorted } from './taskUtils';
import type {
  CurrentUserResponse,
  SavedViewResponse,
  SavedViewSortField,
  TaskItemShareRole,
  WorkspaceResponse,
} from './types';

export function formatWorkspaceRole(
  role: CurrentUserResponse['workspaces'][number]['role'],
  t: Translate,
) {
  if (isOwnerRole(role)) {
    return t('roleOwner');
  }

  return isReadOnlyRole(role) ? t('roleReadOnly') : t('roleMember');
}

export function isOwnerRole(role: CurrentUserResponse['workspaces'][number]['role']) {
  return role === 1 || role === 'Owner';
}

export function isReadOnlyRole(role: CurrentUserResponse['workspaces'][number]['role']) {
  return role === 3 || role === 'ReadOnly' || role === 'Guest';
}

export function formatTaskShareRole(role: TaskItemShareRole, t: Translate) {
  return isReadOnlyTaskShareRole(role)
    ? t('roleReadOnly')
    : t('roleMember');
}

export function isReadOnlyTaskShareRole(role: TaskItemShareRole) {
  return role === 1 || role === 'Viewer' || role === 'ReadOnly';
}

export function isTaskShareWorkspace(workspace: Pick<WorkspaceResponse, 'accessKind'>) {
  return workspace.accessKind === 'TaskShare';
}

export function isSystemAllTasksWorkspace(workspace: Pick<WorkspaceResponse, 'name' | 'accessKind'>) {
  return workspace.accessKind !== 'TaskShare' &&
    workspace.name.trim().toLowerCase() === 'all tasks';
}

export function formatOAuthProvider(provider: string) {
  const normalizedProvider = provider.toLowerCase();
  return normalizedProvider === 'microsoft'
    ? 'Microsoft'
    : provider;
}

export function pickSavedViewId(
  views: SavedViewResponse[],
  preferredViewId: string | null,
) {
  if (preferredViewId && views.some((view) => view.id === preferredViewId)) {
    return preferredViewId;
  }

  return findViewId(views, 'All Tasks') ?? findViewId(views, 'Overview') ?? views[0]?.id ?? null;
}

export function findViewId(views: SavedViewResponse[], name: string) {
  return views.find((view) => view.name.toLowerCase() === name.toLowerCase())?.id ?? null;
}

export function getInitialMode(): WorkspaceMode {
  const requestedMode = new URL(window.location.href).searchParams.get('view');
  return requestedMode === 'templates' || requestedMode === 'tour'
    ? requestedMode
    : 'tasks';
}

export function getInitialViewId() {
  return new URL(window.location.href).searchParams.get('viewId');
}

export function getInitialLanguage(): Language {
  const storedLanguage = window.localStorage.getItem(languageStorageKey);
  return storedLanguage === 'da' ? 'da' : 'en';
}

export function getInitialWorkspaceId() {
  const storedWorkspaceId = window.localStorage.getItem(workspaceStorageKey);
  return storedWorkspaceId && storedWorkspaceId.length > 0 ? storedWorkspaceId : null;
}

export function readStoredStringList(key: string, fallback: string[]) {
  const storedValue = window.localStorage.getItem(key);

  if (!storedValue) {
    return fallback;
  }

  try {
    const parsed = JSON.parse(storedValue) as unknown;
    return Array.isArray(parsed)
      ? uniqueSorted(parsed.filter((value): value is string => typeof value === 'string'))
      : fallback;
  } catch {
    return fallback;
  }
}

export function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError';
}

export function updateUrl(mode: WorkspaceMode, viewId: string | null) {
  const url = new URL(window.location.href);

  if (mode === 'templates' || mode === 'tour') {
    url.searchParams.set('view', mode);
    url.searchParams.delete('viewId');
  } else {
    url.searchParams.delete('view');
    if (viewId) {
      url.searchParams.set('viewId', viewId);
    } else {
      url.searchParams.delete('viewId');
    }
  }

  window.history.replaceState(null, '', url);
}

export function getViewIcon(view: SavedViewResponse): IconName {
  if (view.filter.archive === 'Archived') {
    return 'archive';
  }

  if (view.filter.followUp) {
    return 'clock';
  }

  if (view.filter.status?.toLowerCase().includes('waiting')) {
    return 'waiting';
  }

  if (view.name.toLowerCase().includes('inbox')) {
    return 'inbox';
  }

  return 'list';
}

export function isTextEditingTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tagName = target.tagName.toLowerCase();
  return (
    tagName === 'input' ||
    tagName === 'textarea' ||
    tagName === 'select' ||
    target.isContentEditable
  );
}

export function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

export function buildShareUrl(token: string) {
  const shareUrl = new URL(window.location.href);
  shareUrl.searchParams.set('shareToken', token);
  shareUrl.searchParams.delete('workspaceInvite');
  return shareUrl.toString();
}

export async function copyTextToClipboard(value: string) {
  if (navigator.clipboard) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const textarea = document.createElement('textarea');
  textarea.value = value;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.append(textarea);
  textarea.select();
  document.execCommand('copy');
  textarea.remove();
}

export function formatSortField(value: SavedViewSortField | null | undefined, t: Translate) {
  switch (value) {
    case 'createdAt':
      return t('sortCreated');
    case 'followUpAt':
      return t('sortFollowUp');
    case 'title':
      return t('sortTitle');
    case 'status':
      return t('sortStatus');
    case 'lastTouchedAt':
    default:
      return t('sortLastTouched');
  }
}

export function formatSavedViewName(name: string, t: Translate) {
  switch (name.toLowerCase()) {
    case 'overview':
    case 'all tasks':
      return t('overview');
    case 'archive':
      return t('archive');
    default:
      return name;
  }
}

export function formatWorkspaceName(name: string, t: Translate) {
  return name.trim().toLowerCase() === 'all tasks'
    ? t('overview')
    : name;
}

export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function formatFullDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

export function formatRelativeDate(value: string) {
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

export function toDateInputValue(value: string | null) {
  if (!value) {
    return '';
  }

  return new Date(value).toISOString().slice(0, 10);
}

export function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Unexpected error.';
}
