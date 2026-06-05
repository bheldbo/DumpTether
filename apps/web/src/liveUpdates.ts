import * as signalR from '@microsoft/signalr';

import { getApiBaseUrl, getStoredSessionToken } from './api';

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
): LiveUpdateSubscription {
  const apiBaseUrl = getApiBaseUrl();
  const url = `${apiBaseUrl}/api/live`;
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => getStoredSessionToken() ?? '',
    })
    .withAutomaticReconnect()
    .build();

  connection.on('LiveUpdate', onUpdate);
  connection.onreconnecting((error) => onConnectionLost(error));
  connection.onclose((error) => onConnectionLost(error));
  const startPromise = connection.start().catch((error: Error) => {
    onConnectionLost(error);
  });

  return {
    async joinWorkspace(workspaceId: string) {
      if (!workspaceId) {
        return;
      }

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
      connection.off('LiveUpdate', onUpdate);
      await connection.stop();
    },
  };
}
