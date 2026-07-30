import { formatFullDate } from '../../appUtils';
import { Icon } from '../../components/Icon';
import { type Translate } from '../../localization';
import type { SyncRootResponse } from '../../types';

interface BoardSyncStatusProps {
  syncRoot: SyncRootResponse | null;
  t: Translate;
}

export function BoardSyncStatus({ syncRoot, t }: BoardSyncStatusProps) {
  if (!syncRoot) {
    return null;
  }

  const label = getSyncRootLabel(syncRoot.status, t);
  const details = [
    label,
    syncRoot.lastSyncedAt
      ? `${t('syncLastSynced')}: ${formatFullDate(syncRoot.lastSyncedAt)}`
      : null,
  ].filter(Boolean);

  return (
    <span
      className="board-sync-status"
      data-state={syncRoot.status}
      title={details.join('\n')}
    >
      <Icon name="cloud" />
      <span>{label}</span>
    </span>
  );
}

function getSyncRootLabel(status: string, t: Translate) {
  switch (status) {
    case 'LocalOnly':
      return t('syncRootLocalOnly');
    case 'Linked':
      return t('syncRootLinked');
    case 'Conflict':
      return t('syncConflict');
    case 'AccessRevoked':
      return t('syncRootAccessRevoked');
    default:
      return t('syncUnknown');
  }
}
