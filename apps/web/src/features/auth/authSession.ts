import {
  ApiError,
  getAuthOptions,
  getCurrentUser,
  isDesktopRuntime,
  isTemporarySession,
  listIncomingTaskShares,
  listIncomingWorkspaceInvitations,
  localDesktopLogin,
} from '../../api';
import type {
  AuthClientOptionsResponse,
  CurrentUserResponse,
  TaskShareInboxResponse,
  WorkspaceInvitationInboxResponse,
} from '../../types';

export interface LoadedAuthSession {
  authOptions: AuthClientOptionsResponse;
  currentUser: CurrentUserResponse | null;
  incomingTaskShares: TaskShareInboxResponse[];
  incomingWorkspaceInvitations: WorkspaceInvitationInboxResponse[];
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
    const [incomingWorkspaceInvitations, incomingTaskShares] = await Promise.all([
      listIncomingWorkspaceInvitations().catch(() => []),
      listIncomingTaskShares().catch(() => []),
    ]);

    return {
      authOptions,
      currentUser,
      incomingTaskShares,
      incomingWorkspaceInvitations,
      localDesktopSessionStarted,
      temporarySessionIsActive: isTemporarySession(),
    };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return {
        authOptions,
        currentUser: null,
        incomingTaskShares: [],
        incomingWorkspaceInvitations: [],
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
