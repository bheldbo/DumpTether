import * as signalR from '@microsoft/signalr';

import {
  getApiBaseUrl,
  getCookieAuthCsrfHeader,
  getDesktopBootstrapHeaders,
  getStoredSessionToken,
} from './api';

export interface LiveUpdateMessage {
  eventName: string;
  workspaceId: string;
  taskItemId: string | null;
  timelineEntryId: string | null;
  actorUserId: string | null;
  occurredAt: string;
  updatedAt: string | null;
  recipientUserIds: string[] | null;
}

export interface LiveUpdateSubscription {
  joinWorkspace: (workspaceId: string) => Promise<void>;
  stop: () => Promise<void>;
}

export function startLiveUpdates(
  onUpdate: (message: LiveUpdateMessage) => void,
  onConnectionLost: (error?: Error) => void,
  onReconnected?: () => void,
): LiveUpdateSubscription {
  const apiBaseUrl = getApiBaseUrl();
  const url = `${apiBaseUrl}/api/live`;
  const sessionToken = getStoredSessionToken();
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(url, {
      ...(sessionToken
        ? { accessTokenFactory: () => getStoredSessionToken() ?? '' }
        : {}),
      headers: {
        ...getDesktopBootstrapHeaders(),
        ...(sessionToken ? {} : getCookieAuthCsrfHeader()),
      },
    })
    .withAutomaticReconnect()
    .build();

  connection.on('LiveUpdate', onUpdate);
  connection.onreconnecting((error) => onConnectionLost(error));
  const joinedWorkspaceIds = new Set<string>();
  connection.onreconnected(() => {
    void Promise.all(
      [...joinedWorkspaceIds].map((workspaceId) =>
        connection.invoke('JoinWorkspace', workspaceId)),
    )
      .then(() => onReconnected?.())
      .catch((error: Error) => onConnectionLost(error));
  });
  connection.onclose((error) => onConnectionLost(error));
  let stopWasRequested = false;
  const startPromise = connection.start().catch((error: Error) => {
    if (!stopWasRequested) {
      onConnectionLost(error);
    }
  });

  return {
    async joinWorkspace(workspaceId: string) {
      if (!workspaceId) {
        return;
      }

      joinedWorkspaceIds.add(workspaceId);

      try {
        await startPromise;
        if (connection.state === signalR.HubConnectionState.Connected) {
          await connection.invoke('JoinWorkspace', workspaceId);
        }
      } catch (error) {
        onConnectionLost(error instanceof Error ? error : undefined);
      }
    },
    async stop() {
      stopWasRequested = true;
      connection.off('LiveUpdate', onUpdate);

      try {
        await startPromise;

        if (connection.state !== signalR.HubConnectionState.Disconnected) {
          await connection.stop();
        }
      } catch {
        // The connection may be mid-start during React effect cleanup.
      }
    },
  };
}
