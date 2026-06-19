import { FormEvent, useState } from 'react';
import { Icon } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import { type Translate } from '../../localization';
import type {
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
} from '../../types';

export function ArchiveDialog({
  archiveResolutions,
  bodyText,
  onArchive,
  onClose,
  t,
  taskTitle,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  bodyText?: string;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onClose: () => void;
  t: Translate;
  taskTitle: string;
}) {
  const [archiveResolutionId, setArchiveResolutionId] = useState(
    archiveResolutions[0]?.id ?? '',
  );
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const selectedResolution = archiveResolutions.find(
    (resolution) => resolution.id === archiveResolutionId,
  );
  const noteIsRequired = selectedResolution?.requiresExplanation ?? false;
  const canSubmit = Boolean(archiveResolutionId) && (!noteIsRequired || note.trim());

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!canSubmit) {
      return;
    }

    setIsSubmitting(true);
    await onArchive({
      archiveResolutionId,
      note: note.trim() || null,
    });
    setIsSubmitting(false);
  };

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="archive-dialog-title"
        aria-modal="true"
        className="archive-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('archiveAction')}</p>
            <h2 id="archive-dialog-title">{taskTitle}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('cancel')}</span>
          </button>
        </div>

        <form className="archive-form" onSubmit={handleSubmit}>
          {bodyText ? <p className="resolution-description">{bodyText}</p> : null}
          <label>
            Resolution reason
            <select
              onChange={(event) => setArchiveResolutionId(event.target.value)}
              required
              value={archiveResolutionId}
            >
              {archiveResolutions.length === 0 ? (
                <option value="">No archive reasons available</option>
              ) : null}
              {archiveResolutions.map((resolution) => (
                <option key={resolution.id} value={resolution.id}>
                  {resolution.name}
                </option>
              ))}
            </select>
          </label>

          {selectedResolution?.description ? (
            <p className="resolution-description">{selectedResolution.description}</p>
          ) : null}

          <label>
            Archive note {noteIsRequired ? '(required)' : '(optional)'}
            <textarea
              onChange={(event) => setNote(event.target.value)}
              required={noteIsRequired}
              rows={4}
              value={note}
            />
          </label>

          <div className="dialog-actions">
            <button className="ghost-button" onClick={onClose} type="button">
              {t('cancel')}
            </button>
            <button disabled={!canSubmit || isSubmitting} type="submit">
              {t('archiveAction')}
            </button>
          </div>
        </form>
      </section>
    </ModalFrame>
  );
}

export function ReopenDialog({
  onClose,
  onReopen,
  t,
  taskTitle,
}: {
  onClose: () => void;
  onReopen: (note?: string) => Promise<void>;
  t: Translate;
  taskTitle: string;
}) {
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    try {
      await onReopen(note.trim() || undefined);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="reopen-dialog-title"
        aria-modal="true"
        className="archive-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('unarchiveSelected')}</p>
            <h2 id="reopen-dialog-title">{taskTitle}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('cancel')}</span>
          </button>
        </div>
        <form className="archive-form" onSubmit={handleSubmit}>
          <label>
            {t('unarchiveNote')}
            <textarea
              onChange={(event) => setNote(event.target.value)}
              rows={3}
              value={note}
            />
          </label>
          <div className="dialog-actions">
            <button className="ghost-button" onClick={onClose} type="button">
              {t('cancel')}
            </button>
            <button disabled={isSubmitting} type="submit">
              <Icon name="undo" />
              {t('unarchiveSelected')}
            </button>
          </div>
        </form>
      </section>
    </ModalFrame>
  );
}

export function PermanentDeleteDialog({
  count,
  onClose,
  onDelete,
  t,
}: {
  count: number;
  onClose: () => void;
  onDelete: () => Promise<void>;
  t: Translate;
}) {
  const [isDeleting, setIsDeleting] = useState(false);

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="permanent-delete-title"
        aria-modal="true"
        className="delete-workspace-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('deletePermanently')}</p>
            <h2 id="permanent-delete-title">
              {count} {t('selectedTasks')}
            </h2>
          </div>
          <button className="icon-button" disabled={isDeleting} onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>
        <p>{t('deleteArchivedTasksWarning')}</p>
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
            {t('deletePermanently')}
          </button>
        </div>
      </section>
    </ModalFrame>
  );
}
