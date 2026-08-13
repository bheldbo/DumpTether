import { type FormEvent, useState } from 'react';
import { getErrorMessage } from '../../appUtils';
import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';
import type { ResetPasswordRequest } from '../../types';

export function AccountRecoveryPanel({
  mode,
  onBackToLogin,
  onForgotPassword,
  onResetPassword,
  resetToken,
  t,
}: {
  mode: 'forgot' | 'reset';
  onBackToLogin: () => void;
  onForgotPassword: (email: string) => Promise<void>;
  onResetPassword: (requestBody: ResetPasswordRequest) => Promise<void>;
  resetToken?: string;
  t: Translate;
}) {
  const [email, setEmail] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [requestWasAccepted, setRequestWasAccepted] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      if (mode === 'forgot') {
        await onForgotPassword(email.trim());
        setRequestWasAccepted(true);
      } else if (resetToken) {
        if (newPassword !== confirmPassword) {
          setFormError(t('passwordsDoNotMatch'));
          return;
        }

        await onResetPassword({ token: resetToken, newPassword });
      }
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  if (mode === 'forgot' && requestWasAccepted) {
    return (
      <div className="auth-confirmation-waiting" role="status">
        <span className="auth-confirmation-icon" aria-hidden="true">
          <Icon name="mail" />
        </span>
        <h2>{t('forgotPasswordEmailSentTitle')}</h2>
        <p>{t('forgotPasswordEmailSentBody')}</p>
        <button
          className="secondary-action auth-recovery-back-button"
          onClick={onBackToLogin}
          type="button"
        >
          <Icon name="back" />
          {t('backToLogin')}
        </button>
      </div>
    );
  }

  return (
    <div className="auth-recovery-panel">
      <div className="auth-heading">
        <p className="detail-kicker">{t('accountRecovery')}</p>
        <h2>{mode === 'reset' ? t('resetPasswordTitle') : t('forgotPasswordTitle')}</h2>
        <p>{mode === 'reset' ? t('resetPasswordHelp') : t('forgotPasswordHelp')}</p>
      </div>

      <form className="auth-form" onSubmit={(event) => void submit(event)}>
        {mode === 'forgot' ? (
          <label>
            {t('email')}
            <input
              autoComplete="email"
              onChange={(event) => setEmail(event.target.value)}
              required
              type="email"
              value={email}
            />
          </label>
        ) : (
          <>
            <label>
              {t('newPassword')}
              <input
                autoComplete="new-password"
                minLength={8}
                onChange={(event) => setNewPassword(event.target.value)}
                required
                type="password"
                value={newPassword}
              />
              <small className="form-help">{t('passwordRequirement')}</small>
            </label>
            <label>
              {t('confirmNewPassword')}
              <input
                autoComplete="new-password"
                minLength={8}
                onChange={(event) => setConfirmPassword(event.target.value)}
                required
                type="password"
                value={confirmPassword}
              />
            </label>
          </>
        )}

        <button
          className="auth-submit-button"
          disabled={
            isSubmitting ||
            (mode === 'forgot'
              ? !email.trim()
              : !resetToken || newPassword.length < 8 || confirmPassword.length < 8)
          }
          type="submit"
        >
          <Icon name={mode === 'reset' ? 'check' : 'mail'} />
          {mode === 'reset' ? t('resetPasswordButton') : t('sendResetLink')}
        </button>
      </form>

      <button
        className="secondary-action auth-recovery-back-button"
        disabled={isSubmitting}
        onClick={onBackToLogin}
        type="button"
      >
        <Icon name="back" />
        {t('backToLogin')}
      </button>
      {formError ? <p className="form-error">{formError}</p> : null}
    </div>
  );
}
