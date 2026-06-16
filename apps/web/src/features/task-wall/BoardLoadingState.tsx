import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';

export function BoardLoadingState({ t }: { t: Translate }) {
  return (
    <div className="board-loading-state" role="status" aria-live="polite">
      <span className="board-loading-spinner" aria-hidden="true">
        <Icon name="refresh" />
      </span>
      <div>
        <strong>{t('loadingBoard')}</strong>
        <p>{t('loadingTasks')}</p>
      </div>
    </div>
  );
}
