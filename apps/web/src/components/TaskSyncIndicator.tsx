import { formatFullDate } from '../appUtils';
import { type Translate } from '../localization';
import type { TaskSyncStateResponse } from '../types';
import { Icon } from './Icon';

interface TaskSyncIndicatorProps {
  syncState: TaskSyncStateResponse | null | undefined;
  t: Translate;
}

export function TaskSyncIndicator({ syncState, t }: TaskSyncIndicatorProps) {
  if (!syncState || syncState.status === 'LocalOnly') {
    return null;
  }

  const label = getSyncLabel(syncState, t);

  return (
    <span
      className="task-sync-indicator"
      data-state={syncState.status}
      title={label}
    >
      <Icon name="cloud" />
      <span className="sr-only">{label}</span>
    </span>
  );
}

function getSyncLabel(syncState: TaskSyncStateResponse, t: Translate) {
  const parts = [getSyncStatusLabel(syncState.status, t)];

  if (syncState.lastSyncedAt) {
    parts.push(`${t('syncLastSynced')}: ${formatFullDate(syncState.lastSyncedAt)}`);
  }

  if (syncState.lastAttemptedAt && !syncState.lastSyncedAt) {
    parts.push(`${t('syncLastAttempt')}: ${formatFullDate(syncState.lastAttemptedAt)}`);
  }

  if (syncState.lastError) {
    parts.push(`${t('syncError')}: ${syncState.lastError}`);
  }

  return parts.join('\n');
}

function getSyncStatusLabel(status: string, t: Translate) {
  switch (status) {
    case 'Synced':
      return t('syncSynced');
    case 'Conflict':
      return t('syncConflict');
    case 'Deleted':
      return t('syncDeleted');
    case 'SyncFailed':
      return t('syncFailed');
    default:
      return t('syncUnknown');
  }
}
