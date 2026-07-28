import { useState } from 'react';
import type { Translate } from '../localization';
import type { WorkspaceResponse } from '../types';
import { Icon } from './Icon';
import { ModalFrame } from './ModalFrame';

export function DeleteWorkspaceDialog({
  onClose,
  onDelete,
  t,
  workspace,
}: {
  onClose: () => void;
  onDelete: () => Promise<void>;
  t: Translate;
  workspace: WorkspaceResponse;
}) {
  const [isDeleting, setIsDeleting] = useState(false);

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="delete-workspace-title"
        aria-modal="true"
        className="delete-workspace-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('deleteBoard')}</p>
            <h2 id="delete-workspace-title">{workspace.name}</h2>
          </div>
          <button className="icon-button" disabled={isDeleting} onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>
        <p>{t('deleteBoardConfirmBody')}</p>
        <div className="dialog-actions">
          <button className="ghost-button" disabled={isDeleting} onClick={onClose} type="button">
            {t('cancel')}
          </button>
          <button
            className="danger-action"
            disabled={isDeleting}
            onClick={async () => {
              setIsDeleting(true);
              try {
                await onDelete();
              } finally {
                setIsDeleting(false);
              }
            }}
            type="button"
          >
            <Icon name="trash" />
            {t('deleteBoardNow')}
          </button>
        </div>
      </section>
    </ModalFrame>
  );
}
