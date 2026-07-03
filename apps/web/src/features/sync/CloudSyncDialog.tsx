import { type FormEvent, useState } from 'react';
import { syncCloudBaseUrlStorageKey } from '../../appTypes';
import { Icon } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import { type Translate } from '../../localization';
import type {
  SyncWorkspaceWithCloudRequest,
  SyncWorkspaceWithCloudResponse,
  TaskItemSummaryResponse,
} from '../../types';

interface CloudSyncDialogProps {
  onClose: () => void;
  onSync: (requestBody: SyncWorkspaceWithCloudRequest) => Promise<SyncWorkspaceWithCloudResponse>;
  taskItems: TaskItemSummaryResponse[];
  t: Translate;
  workspaceName: string;
}

export function CloudSyncDialog({
  onClose,
  onSync,
  taskItems,
  t,
  workspaceName,
}: CloudSyncDialogProps) {
  const [cloudApiBaseUrl, setCloudApiBaseUrl] = useState(
    () => window.localStorage.getItem(syncCloudBaseUrlStorageKey) ?? '',
  );
  const [cloudSessionToken, setCloudSessionToken] = useState('');
  const [pushLocalChanges, setPushLocalChanges] = useState(true);
  const [pullRemoteChanges, setPullRemoteChanges] = useState(true);
  const [isSyncing, setIsSyncing] = useState(false);
  const [result, setResult] = useState<SyncWorkspaceWithCloudResponse | null>(null);

  const problemStates = taskItems
    .filter((taskItem) =>
      taskItem.syncState?.status === 'Conflict' ||
      taskItem.syncState?.status === 'SyncFailed')
    .slice(0, 6);

  const submitSync = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    setIsSyncing(true);
    try {
      const response = await onSync({
        cloudApiBaseUrl: cloudApiBaseUrl.trim(),
        cloudSessionToken: cloudSessionToken.trim(),
        pushLocalChanges,
        pullRemoteChanges,
      });
      window.localStorage.setItem(syncCloudBaseUrlStorageKey, cloudApiBaseUrl.trim());
      setCloudSessionToken('');
      setResult(response);
    } finally {
      setIsSyncing(false);
    }
  };

  return (
    <ModalFrame className="dialog-backdrop cloud-sync-backdrop" onClose={onClose}>
      <section
        aria-labelledby="cloud-sync-title"
        aria-modal="true"
        className="cloud-sync-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('cloudSync')}</p>
            <h2 id="cloud-sync-title">{workspaceName}</h2>
          </div>
          <button className="icon-button" disabled={isSyncing} onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>

        <form className="cloud-sync-form" onSubmit={(event) => void submitSync(event)}>
          <label>
            <span>{t('cloudApiUrl')}</span>
            <input
              autoComplete="url"
              onChange={(event) => setCloudApiBaseUrl(event.target.value)}
              placeholder="https://dump.example.com"
              required
              type="url"
              value={cloudApiBaseUrl}
            />
          </label>
          <label>
            <span>{t('cloudSessionToken')}</span>
            <input
              autoComplete="off"
              onChange={(event) => setCloudSessionToken(event.target.value)}
              required
              type="password"
              value={cloudSessionToken}
            />
          </label>
          <p className="context-muted">{t('syncTokenHelp')}</p>

          <div className="cloud-sync-options">
            <label className="checkbox-label">
              <input
                checked={pushLocalChanges}
                onChange={(event) => setPushLocalChanges(event.target.checked)}
                type="checkbox"
              />
              <span>{t('pushLocalChanges')}</span>
            </label>
            <label className="checkbox-label">
              <input
                checked={pullRemoteChanges}
                onChange={(event) => setPullRemoteChanges(event.target.checked)}
                type="checkbox"
              />
              <span>{t('pullCloudChanges')}</span>
            </label>
          </div>

          <div className="dialog-actions">
            <button className="ghost-button" disabled={isSyncing} onClick={onClose} type="button">
              {t('cancel')}
            </button>
            <button
              className="primary-action"
              disabled={isSyncing || (!pushLocalChanges && !pullRemoteChanges)}
              type="submit"
            >
              <Icon name="cloud" />
              {isSyncing ? t('syncing') : t('syncNow')}
            </button>
          </div>
        </form>

        {result ? (
          <div className="cloud-sync-result">
            <h3>{t('syncComplete')}</h3>
            <div className="cloud-sync-stats" aria-label={t('syncStats')}>
              <span>{t('syncPushed')}: {result.pushed}</span>
              <span>{t('syncPulled')}: {result.pulled}</span>
              <span>{t('syncUpdatedLocal')}: {result.updatedLocal}</span>
              <span>{t('syncUpdatedRemote')}: {result.updatedRemote}</span>
              <span>{t('syncConflicts')}: {result.conflicts}</span>
              <span>{t('syncFailedCount')}: {result.failed}</span>
            </div>
            {result.messages.length > 0 ? (
              <ul className="cloud-sync-messages" aria-label={t('syncMessages')}>
                {result.messages.map((message, index) => (
                  <li key={`${index}-${message}`}>{message}</li>
                ))}
              </ul>
            ) : null}
          </div>
        ) : null}

        {problemStates.length > 0 ? (
          <div className="cloud-sync-recovery">
            <h3>{t('syncNeedsReview')}</h3>
            <p>{t('syncRecoveryHelp')}</p>
            <ul>
              {problemStates.map((taskItem) => (
                <li key={taskItem.id}>
                  <span>{taskItem.title}</span>
                  <strong>{taskItem.syncState?.status}</strong>
                </li>
              ))}
            </ul>
          </div>
        ) : null}
      </section>
    </ModalFrame>
  );
}
