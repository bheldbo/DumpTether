import { type FormEvent, useState } from 'react';
import { formatDateTime, getErrorMessage } from '../../appUtils';
import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';
import type { AccountDeletionResponse } from '../../types';

export function AccountDeletionSection({
  accountEmail,
  hasPasswordCredential,
  deletion,
  onCancel,
  onRequest,
  t,
}: {
  accountEmail: string;
  hasPasswordCredential: boolean;
  deletion: AccountDeletionResponse | null;
  onCancel: () => Promise<void>;
  onRequest: (confirmationEmail: string, currentPassword?: string) => Promise<void>;
  t: Translate;
}) {
  const [confirmationEmail, setConfirmationEmail] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const confirmationMatches = confirmationEmail.trim().toLocaleLowerCase() ===
    accountEmail.toLocaleLowerCase();

  const submitRequest = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      await onRequest(confirmationEmail.trim(), currentPassword);
      setConfirmationEmail('');
      setCurrentPassword('');
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const cancelRequest = async () => {
    setFormError(null);
    setIsSubmitting(true);

    try {
      await onCancel();
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <section className="settings-section account-danger-section">
      <div>
        <p className="detail-kicker">{t('dangerZone')}</p>
        <h3>{t('accountDeletion')}</h3>
      </div>
      <p>{t('accountDeletionHelp')}</p>

      {deletion ? (
        <div className="account-deletion-schedule">
          <Icon name="clock" />
          <div>
            <strong>{t('accountDeletionScheduled')}</strong>
            <p>{t('accountDeletionScheduledFor')}: {formatDateTime(deletion.scheduledFor)}</p>
            <small>{t('accountDeletionRequestedAt')}: {formatDateTime(deletion.requestedAt)}</small>
            {deletion.reminderSentAt ? (
              <small>{t('accountDeletionReminderSent')}: {formatDateTime(deletion.reminderSentAt)}</small>
            ) : null}
          </div>
          <button
            className="secondary-action"
            disabled={isSubmitting}
            onClick={() => void cancelRequest()}
            type="button"
          >
            <Icon name="undo" />
            {t('cancelAccountDeletion')}
          </button>
        </div>
      ) : (
        <form className="account-deletion-form" onSubmit={(event) => void submitRequest(event)}>
          <label>
            {t('confirmAccountEmail')}
            <input
              autoComplete="email"
              onChange={(event) => setConfirmationEmail(event.target.value)}
              placeholder={accountEmail}
              type="email"
              value={confirmationEmail}
            />
          </label>
          <p className="form-help">{t('accountDeletion48HourHelp')}</p>
          {hasPasswordCredential ? (
            <label>
              {t('currentPassword')}
              <input
                autoComplete="current-password"
                onChange={(event) => setCurrentPassword(event.target.value)}
                required
                type="password"
                value={currentPassword}
              />
            </label>
          ) : (
            <p className="form-help">{t('accountDeletionRecentLoginHelp')}</p>
          )}
          <button
            className="danger-action"
            disabled={
              !confirmationMatches ||
              isSubmitting ||
              (hasPasswordCredential && currentPassword.length < 8)
            }
            type="submit"
          >
            <Icon name="trash" />
            {t('scheduleAccountDeletion')}
          </button>
        </form>
      )}

      {formError ? <p className="form-error">{formError}</p> : null}
    </section>
  );
}
