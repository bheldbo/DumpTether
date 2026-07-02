import {
  ApiError,
  getAuthOptions,
  getCurrentUser,
  isDesktopRuntime,
  isTemporarySession,
  listAuthSessions,
  listIncomingTaskShares,
  listIncomingWorkspaceInvitations,
  localDesktopLogin,
} from '../../api';
import type {
  AuthClientOptionsResponse,
  AuthSessionListItemResponse,
  CurrentUserResponse,
  TaskShareInboxResponse,
  WorkspaceInvitationInboxResponse,
} from '../../types';

export interface LoadedAuthSession {
  authOptions: AuthClientOptionsResponse;
  currentUser: CurrentUserResponse | null;
  authSessions: AuthSessionListItemResponse[];
  incomingTaskShares: TaskShareInboxResponse[];
  incomingWorkspaceInvitations: WorkspaceInvitationInboxResponse[];
  localDesktopSessionIsActive: boolean;
  localDesktopSessionStarted: boolean;
  temporarySessionIsActive: boolean;
}

export async function loadAuthSession(): Promise<LoadedAuthSession> {
  const authOptions = await getAuthOptions();

  try {
    return await loadAuthenticatedSession(authOptions, false);
  } catch (error) {
    if (!shouldStartDesktopLocalSession(authOptions, error)) {
      throw error;
    }

    await localDesktopLogin();
    return await loadAuthenticatedSession(authOptions, true);
  }
}

async function loadAuthenticatedSession(
  authOptions: AuthClientOptionsResponse,
  localDesktopSessionStarted: boolean,
): Promise<LoadedAuthSession> {
  try {
    const currentUser = await getCurrentUser();
    const [authSessions, incomingWorkspaceInvitations, incomingTaskShares] = await Promise.all([
      listAuthSessions().catch(() => []),
      listIncomingWorkspaceInvitations().catch(() => []),
      listIncomingTaskShares().catch(() => []),
    ]);

    return {
      authOptions,
      authSessions,
      currentUser,
      incomingTaskShares,
      incomingWorkspaceInvitations,
      localDesktopSessionIsActive: isLocalDesktopSession(currentUser),
      localDesktopSessionStarted,
      temporarySessionIsActive: isGuestSession(currentUser) || isTemporarySession(),
    };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return {
        authOptions,
        authSessions: [],
        currentUser: null,
        incomingTaskShares: [],
        incomingWorkspaceInvitations: [],
        localDesktopSessionIsActive: false,
        localDesktopSessionStarted: false,
        temporarySessionIsActive: false,
      };
    }

    throw error;
  }
}

function shouldStartDesktopLocalSession(
  authOptions: AuthClientOptionsResponse,
  error: unknown,
) {
  return authOptions.requiresAuthentication &&
    isDesktopRuntime() &&
    error instanceof ApiError &&
    error.status === 401;
}

function isLocalDesktopSession(currentUser: CurrentUserResponse | null) {
  return currentUser?.session.sessionType === 'DesktopLocal' ||
    currentUser?.session.sessionType === 2;
}

function isGuestSession(currentUser: CurrentUserResponse | null) {
  return currentUser?.session.sessionType === 'Guest' ||
    currentUser?.session.sessionType === 5;
}
