import type {
  ArchiveResolutionResponse,
  ProjectResponse,
  SavedViewResponse,
  SyncRootResponse,
  TaskItemSummaryResponse,
  TaskTemplateDetailResponse,
  WorkspaceInvitationResponse,
  WorkspaceMemberResponse,
  WorkspaceResponse,
} from './types';

export interface CachedWorkspaceSnapshot {
  archiveResolutions: ArchiveResolutionResponse[];
  currentViewId: string | null;
  knownStatuses: string[];
  projects: ProjectResponse[];
  savedViews: SavedViewResponse[];
  taskColorOptions: string[];
  taskItems: TaskItemSummaryResponse[];
  templates: TaskTemplateDetailResponse[];
  syncRoots: SyncRootResponse[];
  viewCounts: Record<string, number>;
  workspace: WorkspaceResponse | null;
  workspaceInvitations: WorkspaceInvitationResponse[];
  workspaceMembers: WorkspaceMemberResponse[];
  workspaces: WorkspaceResponse[];
}

const workspaceSnapshotMemoryCache = new Map<string, CachedWorkspaceSnapshot>();

export function buildWorkspaceCacheKey(workspaceId: string, viewId: string, userId: string) {
  return `dumptether.cache.${userId}.${workspaceId}.${viewId}`;
}

export function readCachedWorkspaceSnapshot(key: string): CachedWorkspaceSnapshot | null {
  const memorySnapshot = workspaceSnapshotMemoryCache.get(key);
  if (memorySnapshot) {
    return memorySnapshot;
  }

  const storedValue = window.sessionStorage.getItem(key);

  if (!storedValue) {
    return null;
  }

  try {
    const parsed = JSON.parse(storedValue) as CachedWorkspaceSnapshot & {
      cachedAt?: string;
    };
    workspaceSnapshotMemoryCache.set(key, parsed);
    return parsed;
  } catch {
    window.sessionStorage.removeItem(key);
    return null;
  }
}

export function writeCachedWorkspaceSnapshot(key: string, snapshot: CachedWorkspaceSnapshot) {
  workspaceSnapshotMemoryCache.set(key, snapshot);
  window.sessionStorage.setItem(
    key,
    JSON.stringify({
      ...snapshot,
      cachedAt: new Date().toISOString(),
    }),
  );
}
