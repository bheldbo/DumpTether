import { formatFullDate } from '../appUtils';
import { type Translate } from '../localization';
import type { TaskSyncStateResponse } from '../types';
import { Icon } from './Icon';

interface TaskSyncIndicatorProps {
  onRetry?: () => void;
  syncState: TaskSyncStateResponse | null | undefined;
  t: Translate;
}

export function TaskSyncIndicator({ onRetry, syncState, t }: TaskSyncIndicatorProps) {
  if (!syncState || syncState.status === 'LocalOnly') {
    return null;
  }

  const label = getSyncLabel(syncState, t);
  const canRetry = Boolean(onRetry) &&
    (syncState.status === 'SyncFailed' || syncState.status === 'Conflict');

  if (canRetry) {
    return (
      <button
        className="task-sync-indicator task-sync-retry"
        data-state={syncState.status}
        onClick={(event) => {
          event.stopPropagation();
          onRetry?.();
        }}
        title={`${label}\n${t('retrySync')}`}
        type="button"
      >
        <Icon name="cloud" />
        <span className="sr-only">{t('retrySync')}</span>
      </button>
    );
  }

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
