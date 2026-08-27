import { type FormEvent, useEffect, useState } from 'react';
import { beginOAuthLogin } from '../../api';
import type { SettingsSectionKey, StatusOption } from '../../appTypes';
import { readCloudSyncApiBaseUrl } from '../../appSettings';
import { Icon, type IconName } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import { DeleteWorkspaceDialog } from '../../components/DeleteWorkspaceDialog';
import { AccountRecoveryPanel } from '../auth/AccountRecoveryPanel';
import {
  LegalNoticeDialog,
  MicrosoftMark,
  type LegalDocumentKind,
} from '../auth/LegalNoticeDialog';
import { AccountDeletionSection } from './AccountDeletionSection';
import { HelpSection } from './HelpSection';
import { NotificationPreferencesSection } from './NotificationPreferencesSection';
import {
  formatDateTime,
  formatOAuthProvider,
  formatWorkspaceRole,
  getErrorMessage,
} from '../../appUtils';
import type { Language, Translate } from '../../localization';
import type {
  AccountDeletionResponse,
  AccountNotificationPreferencesResponse,
  AuthSessionListItemResponse,
  AuthClientOptionsResponse,
  CloudSyncAccountResponse,
  ConnectCloudAccountRequest,
  CurrentUserResponse,
  LoginUserRequest,
  RegisterUserRequest,
  RegisterUserResponse,
  ResetPasswordRequest,
  TaskShareInboxResponse,
  UpdateAccountNotificationPreferencesRequest,
  WorkspaceInvitationInboxResponse,
  WorkspaceResponse,
} from '../../types';

export function AuthPanel({
  authOptions,
  currentUser,
  isLoading,
  localDesktopSessionIsActive,
  onDevelopmentLogin,
  onGuestLogin,
  onForgotPassword,
  onLogin,
  onLogout,
  onRegister,
  onResetPassword,
  onResetPasswordComplete,
  resetPasswordToken,
  temporarySessionIsActive,
  t,
  variant,
}: {
  authOptions: AuthClientOptionsResponse;
  currentUser: CurrentUserResponse | null;
  isLoading: boolean;
  localDesktopSessionIsActive: boolean;
  onDevelopmentLogin: () => Promise<void>;
  onGuestLogin: () => Promise<void>;
  onForgotPassword: (email: string) => Promise<void>;
  onLogin: (requestBody: LoginUserRequest) => Promise<void>;
  onLogout?: () => Promise<void>;
  onRegister: (requestBody: RegisterUserRequest) => Promise<RegisterUserResponse>;
  onResetPassword: (requestBody: ResetPasswordRequest) => Promise<void>;
  onResetPasswordComplete: () => void;
  resetPasswordToken: string | null;
  temporarySessionIsActive: boolean;
  t: Translate;
  variant: 'gate' | 'settings';
}) {
  const [mode, setMode] = useState<'login' | 'register' | 'forgot'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [inviteCode, setInviteCode] = useState('');
  const [legalAccepted, setLegalAccepted] = useState(false);
  const [openLegalDocument, setOpenLegalDocument] = useState<LegalDocumentKind | null>(null);
  const [pendingConfirmationEmail, setPendingConfirmationEmail] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const signupMode = normalizeSignupMode(authOptions.signupMode);
  const registrationIsAvailable = signupMode !== 'Closed';
  const registrationNeedsInvite = signupMode === 'InviteOnly';
  const legalNoticesAvailable = Boolean(
    authOptions.legal.termsVersion && authOptions.legal.privacyNoticeVersion,
  );
  const legalAcceptance = authOptions.legal.acceptanceRequired
    ? {
        termsAccepted: legalAccepted,
        termsVersion: authOptions.legal.termsVersion,
        privacyNoticeAcknowledged: legalAccepted,
        privacyNoticeVersion: authOptions.legal.privacyNoticeVersion,
      }
    : null;
  const oAuthLegalAcceptance = authOptions.legal.acceptanceRequired && mode === 'register'
    ? {
        termsAccepted: legalAccepted,
        termsVersion: authOptions.legal.termsVersion,
        privacyNoticeAcknowledged: legalAccepted,
        privacyNoticeVersion: authOptions.legal.privacyNoticeVersion,
      }
    : null;

  useEffect(() => {
    if (!registrationIsAvailable && mode === 'register') {
      setMode('login');
    }
  }, [mode, registrationIsAvailable]);

  const submitAuthForm = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    setStatusMessage(null);
    setIsSubmitting(true);

    try {
      if (mode === 'register') {
        const registered = await onRegister({
          email: email.trim(),
          password,
          displayName: displayName.trim() || null,
          inviteCode: registrationNeedsInvite ? inviteCode.trim() : null,
          legalAcceptance,
        });
        if (registered.emailConfirmationRequired) {
          setPendingConfirmationEmail(email.trim());
          setStatusMessage(null);
        } else {
          setStatusMessage(t('authRegistered'));
        }
      } else {
        await onLogin({
          email: email.trim(),
          password,
          deviceName: 'web browser',
        });
        setStatusMessage(t('authLoggedIn'));
      }

      setPassword('');
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitDevelopmentLogin = async () => {
    setFormError(null);
    setStatusMessage(null);
    setIsSubmitting(true);

    try {
      await onDevelopmentLogin();
      setStatusMessage(t('authLoggedIn'));
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitGuestLogin = async () => {
    setFormError(null);
    setStatusMessage(null);
    setIsSubmitting(true);

    try {
      await onGuestLogin();
      setStatusMessage(t('guestModeToast'));
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const startOAuthLogin = (provider: string) => {
    setFormError(null);
    beginOAuthLogin(provider, oAuthLegalAcceptance);
  };

  const wrapperClassName = variant === 'gate'
    ? 'auth-gate'
    : 'settings-section auth-panel';
  const canSubmit = email.trim().length > 0 &&
    password.length >= 8 &&
    (mode !== 'register' || registrationIsAvailable) &&
    (!registrationNeedsInvite || inviteCode.trim().length > 0) &&
    (mode !== 'register' || !authOptions.legal.acceptanceRequired || legalAccepted);
  const canStartOAuth = mode !== 'register' || (
    registrationIsAvailable &&
    (!authOptions.legal.acceptanceRequired || legalAccepted)
  );

  if (resetPasswordToken) {
    return (
      <section className={wrapperClassName} aria-label={t('accountRecovery')}>
        {variant === 'gate' ? (
          <img
            alt="DumpTether"
            className="auth-product-logo"
            src="/assets/dumptether-logo.png"
          />
        ) : null}
        <AccountRecoveryPanel
          mode="reset"
          onBackToLogin={onResetPasswordComplete}
          onForgotPassword={onForgotPassword}
          onResetPassword={async (requestBody) => {
            await onResetPassword(requestBody);
            onResetPasswordComplete();
          }}
          resetToken={resetPasswordToken}
          t={t}
        />
      </section>
    );
  }

  if (currentUser) {
    const displayName = localDesktopSessionIsActive
      ? t('localDesktopModeShort')
      : currentUser.user.displayName || currentUser.user.email;

    return (
      <section className={wrapperClassName} aria-label={t('account')}>
        <div className="auth-heading">
          <p className="detail-kicker">{t('account')}</p>
          <h2 className={localDesktopSessionIsActive ? 'local-mode-chip' : undefined}>
            {displayName}
          </h2>
          {!localDesktopSessionIsActive ? (
            <p>{t('signedInAs')}: {currentUser.user.email}</p>
          ) : null}
        </div>
        {temporarySessionIsActive ? (
          <p className="guest-warning">{t('guestModePersistent')}</p>
        ) : localDesktopSessionIsActive ? (
          <p className="guest-warning">{t('localDesktopModePersistent')}</p>
        ) : null}
        <div className="auth-workspace-list">
          {currentUser.workspaces.map((workspaceItem) => (
            <span className="auth-workspace-chip" key={workspaceItem.id}>
              <span
                className="workspace-color-dot"
                style={{ backgroundColor: workspaceItem.color ?? '#184c48' }}
              />
              {workspaceItem.name}
              <strong>{formatWorkspaceRole(workspaceItem.role, t)}</strong>
            </span>
          ))}
        </div>
        {onLogout && !localDesktopSessionIsActive ? (
          <button
            className="secondary-action logout-button"
            disabled={isSubmitting}
            onClick={async () => {
              setIsSubmitting(true);
              try {
                await onLogout();
              } catch (error) {
                setFormError(getErrorMessage(error));
              } finally {
                setIsSubmitting(false);
              }
            }}
            type="button"
          >
            <Icon name="logout" />
            {t('logout')}
          </button>
        ) : null}
        {legalNoticesAvailable ? (
          <nav aria-label={t('legalInformation')} className="auth-legal-links">
            <button onClick={() => setOpenLegalDocument('terms')} type="button">
              {t('termsOfUse')}
            </button>
            <span aria-hidden="true">·</span>
            <button onClick={() => setOpenLegalDocument('privacy')} type="button">
              {t('privacyNotice')}
            </button>
          </nav>
        ) : null}
        {formError ? <p className="form-error">{formError}</p> : null}
        {openLegalDocument ? (
          <LegalNoticeDialog
            kind={openLegalDocument}
            legal={authOptions.legal}
            onClose={() => setOpenLegalDocument(null)}
            t={t}
          />
        ) : null}
      </section>
    );
  }

  if (pendingConfirmationEmail) {
    return (
      <section className={wrapperClassName} aria-label={t('account')}>
        {variant === 'gate' ? (
          <img
            alt="DumpTether"
            className="auth-product-logo"
            src="/assets/dumptether-logo.png"
          />
        ) : null}
        <div className="auth-confirmation-waiting" role="status">
          <span className="auth-confirmation-icon" aria-hidden="true">
            <Icon name="mail" />
          </span>
          <p className="detail-kicker">{t('emailConfirmationSent')}</p>
          <h2>{t('checkEmailTitle')}</h2>
          <p>{t('checkEmailBody')}</p>
          <strong>{pendingConfirmationEmail}</strong>
          <p className="form-help">{t('checkEmailDeliveryHelp')}</p>
          <button
            className="auth-submit-button auth-confirmation-login-button"
            onClick={() => {
              setPendingConfirmationEmail(null);
              setMode('login');
              setStatusMessage(null);
              setFormError(null);
            }}
            type="button"
          >
            <Icon name="login" />
            {t('backToLogin')}
          </button>
        </div>
      </section>
    );
  }

  if (mode === 'forgot') {
    return (
      <section className={wrapperClassName} aria-label={t('accountRecovery')}>
        {variant === 'gate' ? (
          <img
            alt="DumpTether"
            className="auth-product-logo"
            src="/assets/dumptether-logo.png"
          />
        ) : null}
        <AccountRecoveryPanel
          mode="forgot"
          onBackToLogin={() => setMode('login')}
          onForgotPassword={onForgotPassword}
          onResetPassword={onResetPassword}
          t={t}
        />
      </section>
    );
  }

  return (
    <section className={wrapperClassName} aria-label={t('account')}>
      {variant === 'gate' ? (
        <img
          alt="DumpTether"
          className="auth-product-logo"
          src="/assets/dumptether-logo.png"
        />
      ) : null}
      <div className="auth-heading">
        <p className="detail-kicker">{t('account')}</p>
        <h2>{variant === 'gate' ? t('authRequiredTitle') : t('notSignedIn')}</h2>
        <p>{variant === 'gate' ? t('authRequiredBody') : t('authSettingsHelp')}</p>
      </div>

      <div className="auth-mode-toggle" role="group" aria-label={t('account')}>
        <button
          aria-pressed={mode === 'login'}
          onClick={() => setMode('login')}
          type="button"
        >
          {t('login')}
        </button>
        <button
          aria-pressed={mode === 'register'}
          disabled={!registrationIsAvailable}
          onClick={() => setMode('register')}
          type="button"
        >
          {t('register')}
        </button>
      </div>
      {signupMode === 'Closed' ? (
        <p className="form-help">{t('signupClosed')}</p>
      ) : signupMode === 'InviteOnly' ? (
        <p className="form-help">{t('signupInviteOnlyHelp')}</p>
      ) : signupMode === 'Whitelist' ? (
        <p className="form-help">{t('signupWhitelistHelp')}</p>
      ) : null}

      <form className="auth-form" onSubmit={(event) => void submitAuthForm(event)}>
        {mode === 'register' ? (
          <label>
            {t('displayName')}
            <input
              autoComplete="name"
              onChange={(event) => setDisplayName(event.target.value)}
              type="text"
              value={displayName}
            />
          </label>
        ) : null}

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

        <label>
          {t('password')}
          <input
            autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
            minLength={8}
            onChange={(event) => setPassword(event.target.value)}
            required
            type="password"
            value={password}
          />
          {mode === 'register' ? (
            <small className="form-help">{t('passwordRequirement')}</small>
          ) : null}
        </label>

        {mode === 'login' && authOptions.passwordRecoveryEnabled ? (
          <button
            className="auth-forgot-password-link"
            onClick={() => {
              setFormError(null);
              setStatusMessage(null);
              setMode('forgot');
            }}
            type="button"
          >
            {t('forgotPassword')}
          </button>
        ) : null}

        {mode === 'register' && registrationNeedsInvite ? (
          <label>
            {t('inviteCode')}
            <input
              autoComplete="one-time-code"
              onChange={(event) => setInviteCode(event.target.value)}
              required
              type="text"
              value={inviteCode}
            />
          </label>
        ) : null}

        {mode === 'register' && authOptions.legal.acceptanceRequired ? (
          <div className="legal-acceptance-row">
            <input
              checked={legalAccepted}
              id={`legal-acceptance-${variant}`}
              onChange={(event) => setLegalAccepted(event.target.checked)}
              type="checkbox"
            />
            <p>
              <label htmlFor={`legal-acceptance-${variant}`}>
                {t('legalAgreementPrefix')}{' '}
              </label>
              <button onClick={() => setOpenLegalDocument('terms')} type="button">
                {t('termsOfUse')}
              </button>
              {' '}{t('legalAgreementJoin')}{' '}
              <button onClick={() => setOpenLegalDocument('privacy')} type="button">
                {t('privacyNotice')}
              </button>.
            </p>
          </div>
        ) : null}

        <button
          className="auth-submit-button"
          disabled={!canSubmit || isSubmitting || isLoading}
          type="submit"
        >
          <Icon name={mode === 'register' ? 'user' : 'login'} />
          {mode === 'register' ? t('registerButton') : t('loginButton')}
        </button>
      </form>

      {authOptions.oAuthProviders.length > 0 ? (
        <>
          <div className="auth-divider"><span>{t('or')}</span></div>
          <div className="oauth-login-list">
            {authOptions.oAuthProviders.map((provider) => (
              <button
                className="oauth-provider-button"
                disabled={isSubmitting || isLoading || !canStartOAuth}
                key={provider}
                onClick={() => startOAuthLogin(provider)}
                type="button"
              >
                {provider.toLowerCase() === 'microsoft' ? <MicrosoftMark /> : null}
                {mode === 'register' ? t('registerWith') : t('continueWith')}{' '}
                {formatOAuthProvider(provider)}
              </button>
            ))}
          </div>
        </>
      ) : null}

      {legalNoticesAvailable && (mode !== 'register' || !authOptions.legal.acceptanceRequired) ? (
        <nav aria-label={t('legalInformation')} className="auth-legal-links">
          <button onClick={() => setOpenLegalDocument('terms')} type="button">
            {t('termsOfUse')}
          </button>
          <span aria-hidden="true">·</span>
          <button onClick={() => setOpenLegalDocument('privacy')} type="button">
            {t('privacyNotice')}
          </button>
        </nav>
      ) : null}

      {authOptions.developmentLoginEnabled ? (
        <div className="dev-login-panel">
          <button
            className="secondary-action"
            disabled={isSubmitting || isLoading}
            onClick={() => void submitDevelopmentLogin()}
            type="button"
          >
            {t('useDevelopmentAccount')}
          </button>
          <p>{t('developmentAccountHelp')}</p>
        </div>
      ) : null}

      {authOptions.guestSessionsEnabled ? (
        <div className="dev-login-panel">
          <button
            className="ghost-button auth-secondary-button"
            disabled={isSubmitting || isLoading}
            onClick={() => void submitGuestLogin()}
            type="button"
          >
            <Icon name="user" />
            {t('continueWithoutAccount')}
          </button>
          <p>{t('continueWithoutAccountHelp')}</p>
        </div>
      ) : null}

      {statusMessage ? <p className="form-success">{statusMessage}</p> : null}
      {formError ? <p className="form-error">{formError}</p> : null}
      {openLegalDocument ? (
        <LegalNoticeDialog
          kind={openLegalDocument}
          legal={authOptions.legal}
          onClose={() => setOpenLegalDocument(null)}
          t={t}
        />
      ) : null}
    </section>
  );
}
export function AccountPanel({
  accountDeletion,
  accountNotificationPreferences,
  authSessions,
  authOptions,
  cloudSyncAccount,
  currentUser,
  incomingTaskShares,
  incomingWorkspaceInvitations,
  isLoadingAuth,
  localDesktopSessionIsActive,
  onAcceptIncomingWorkspaceInvitation,
  onCancelAccountDeletion,
  onConnectCloudAccount,
  onClose,
  onDeclineIncomingWorkspaceInvitation,
  onDisconnectCloudAccount,
  onDevelopmentLogin,
  onGuestLogin,
  onForgotPassword,
  onLeaveTaskShare,
  onLogin,
  onLogout,
  onOpenTour,
  onUpdateAccountNotificationPreferences,
  onRequestAccountDeletion,
  onRegister,
  onRevokeAuthSession,
  onResetPassword,
  onResetPasswordComplete,
  resetPasswordToken,
  temporarySessionIsActive,
  t,
}: {
  accountDeletion: AccountDeletionResponse | null;
  accountNotificationPreferences: AccountNotificationPreferencesResponse | null;
  authSessions: AuthSessionListItemResponse[];
  authOptions: AuthClientOptionsResponse;
  cloudSyncAccount: CloudSyncAccountResponse | null;
  currentUser: CurrentUserResponse | null;
  incomingTaskShares: TaskShareInboxResponse[];
  incomingWorkspaceInvitations: WorkspaceInvitationInboxResponse[];
  isLoadingAuth: boolean;
  localDesktopSessionIsActive: boolean;
  onAcceptIncomingWorkspaceInvitation: (id: string) => Promise<void>;
  onCancelAccountDeletion: () => Promise<void>;
  onConnectCloudAccount: (requestBody: ConnectCloudAccountRequest) => Promise<void>;
  onClose: () => void;
  onDeclineIncomingWorkspaceInvitation: (id: string) => Promise<void>;
  onDisconnectCloudAccount: () => Promise<void>;
  onDevelopmentLogin: () => Promise<void>;
  onGuestLogin: () => Promise<void>;
  onForgotPassword: (email: string) => Promise<void>;
  onLeaveTaskShare: (shareId: string) => Promise<void>;
  onLogin: (requestBody: LoginUserRequest) => Promise<void>;
  onLogout: () => Promise<void>;
  onOpenTour: () => void;
  onUpdateAccountNotificationPreferences: (
    request: UpdateAccountNotificationPreferencesRequest,
  ) => Promise<AccountNotificationPreferencesResponse>;
  onRequestAccountDeletion: (
    confirmationEmail: string,
    currentPassword?: string,
  ) => Promise<void>;
  onRegister: (requestBody: RegisterUserRequest) => Promise<RegisterUserResponse>;
  onRevokeAuthSession: (sessionId: string) => Promise<void>;
  onResetPassword: (requestBody: ResetPasswordRequest) => Promise<void>;
  onResetPasswordComplete: () => void;
  resetPasswordToken: string | null;
  temporarySessionIsActive: boolean;
  t: Translate;
}) {
  const visibleSessions = authSessions.filter(
    (session) =>
      !session.revokedAt &&
      session.sessionType !== 'DesktopLocal' &&
      session.sessionType !== 2,
  );

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="account-title"
        aria-modal="true"
        className="account-panel"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">DumpTether</p>
            <h2 id="account-title">{t('account')}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>

        <AuthPanel
          authOptions={authOptions}
          currentUser={currentUser}
          isLoading={isLoadingAuth}
          localDesktopSessionIsActive={localDesktopSessionIsActive}
          onDevelopmentLogin={onDevelopmentLogin}
          onForgotPassword={onForgotPassword}
          onGuestLogin={onGuestLogin}
          onLogin={onLogin}
          onLogout={onLogout}
          onRegister={onRegister}
          onResetPassword={onResetPassword}
          onResetPasswordComplete={onResetPasswordComplete}
          resetPasswordToken={resetPasswordToken}
          temporarySessionIsActive={temporarySessionIsActive}
          t={t}
          variant="settings"
        />

        {localDesktopSessionIsActive ? (
          <CloudAccountSection
            cloudSyncAccount={cloudSyncAccount}
            onConnectCloudAccount={onConnectCloudAccount}
            onDisconnectCloudAccount={onDisconnectCloudAccount}
            t={t}
          />
        ) : null}

        {currentUser && visibleSessions.length > 0 ? (
          <section className="settings-section">
            <h3>{t('sessions')}</h3>
            <div className="account-notification-list">
              {visibleSessions.map((session) => (
                    <article className="account-notification-card" key={session.id}>
                      <Icon name={sessionIcon(session.sessionType)} />
                      <div>
                        <strong>
                          {formatSessionType(session.sessionType, t)}
                          {session.isCurrent ? ` (${t('currentSession')})` : ''}
                        </strong>
                        <p>{session.deviceName || t('unknownDevice')}</p>
                        <div className="session-metadata">
                          <small title={formatDateTime(session.createdAt)}>
                            {t('created')}: {formatDateTime(session.createdAt)}
                          </small>
                          <small>{t('expires')}: {formatDateTime(session.expiresAt)}</small>
                        </div>
                      </div>
                      {!session.isCurrent ? (
                        <button
                          className="secondary-action logout-button"
                          onClick={() => void onRevokeAuthSession(session.id)}
                          type="button"
                        >
                          <Icon name="logout" />
                          {t('revokeSession')}
                        </button>
                      ) : null}
                    </article>
                  ))}
            </div>
          </section>
        ) : null}

        {currentUser ? (
          <section className="settings-section">
            <h3>{t('notifications')}</h3>
            {incomingWorkspaceInvitations.length === 0 && incomingTaskShares.length === 0 ? (
              <p>{t('noIncomingNotifications')}</p>
            ) : (
              <div className="account-notification-list">
                {incomingWorkspaceInvitations.map((invitation) => (
                  <article className="account-notification-card" key={invitation.id}>
                    <span
                      className="workspace-color-dot"
                      style={{ backgroundColor: invitation.workspaceColor ?? '#184c48' }}
                    />
                    <div>
                      <strong>{invitation.workspaceName}</strong>
                      <p>
                        {t('invitedBy')} {invitation.invitedByDisplayName || invitation.invitedByEmail}
                        {' '}({formatWorkspaceRole(invitation.role, t)})
                      </p>
                    </div>
                    <div className="notification-actions">
                      <button
                        className="secondary-action"
                        onClick={() => void onAcceptIncomingWorkspaceInvitation(invitation.id)}
                        type="button"
                      >
                        <Icon name="check" />
                        {t('acceptInvite')}
                      </button>
                      <button
                        className="ghost-button"
                        onClick={() => void onDeclineIncomingWorkspaceInvitation(invitation.id)}
                        type="button"
                      >
                        {t('declineInvite')}
                      </button>
                    </div>
                  </article>
                ))}
                {incomingTaskShares.map((share) => (
                  <article className="account-notification-card" key={share.shareId}>
                    <span
                      className="workspace-color-dot"
                      style={{ backgroundColor: share.workspaceColor ?? '#184c48' }}
                    />
                    <div>
                      <strong>{share.taskTitle}</strong>
                      <p>
                        {t('sharedBy')} {share.sharedByDisplayName || share.sharedByEmail}
                        {' '} - {share.workspaceName}
                      </p>
                    </div>
                    <div className="notification-actions">
                      <button
                        className="ghost-button"
                        onClick={() => void onLeaveTaskShare(share.shareId)}
                        type="button"
                      >
                        {t('leaveTaskShare')}
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>
        ) : null}

        {currentUser &&
        !localDesktopSessionIsActive &&
        !temporarySessionIsActive &&
        accountNotificationPreferences ? (
          <NotificationPreferencesSection
            onUpdate={onUpdateAccountNotificationPreferences}
            preferences={accountNotificationPreferences}
            t={t}
          />
        ) : null}

        {currentUser ? <HelpSection onStartTour={onOpenTour} t={t} /> : null}

        {currentUser && authOptions.accountDeletionEnabled && !localDesktopSessionIsActive && !temporarySessionIsActive ? (
          <AccountDeletionSection
            accountEmail={currentUser.user.email}
            hasPasswordCredential={currentUser.user.hasPasswordCredential}
            deletion={accountDeletion}
            onCancel={onCancelAccountDeletion}
            onRequest={onRequestAccountDeletion}
            t={t}
          />
        ) : null}

      </section>
    </ModalFrame>
  );
}
function CloudAccountSection({
  cloudSyncAccount,
  onConnectCloudAccount,
  onDisconnectCloudAccount,
  t,
}: {
  cloudSyncAccount: CloudSyncAccountResponse | null;
  onConnectCloudAccount: (requestBody: ConnectCloudAccountRequest) => Promise<void>;
  onDisconnectCloudAccount: () => Promise<void>;
  t: Translate;
}) {
  const cloudApiBaseUrl = readCloudSyncApiBaseUrl();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      await onConnectCloudAccount({
        cloudApiBaseUrl,
        email,
        password,
        deviceName: 'DumpTether desktop',
      });
      setPassword('');
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const disconnect = async () => {
    setFormError(null);
    setIsSubmitting(true);

    try {
      await onDisconnectCloudAccount();
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <section className="settings-section">
      <h3>{t('cloudAccount')}</h3>
      <p>{t('cloudAccountHelp')}</p>

      {cloudSyncAccount?.isConnected ? (
        <div className="account-notification-card cloud-account-card">
          <Icon name="cloud" />
          <div>
            <strong>
              {t('sessionDesktopCloud')} · {
                cloudSyncAccount.cloudDisplayName || cloudSyncAccount.cloudEmail
              }
            </strong>
            <p>{cloudSyncAccount.cloudEmail}</p>
            <div className="session-metadata">
              <small>{cloudSyncAccount.cloudApiBaseUrl}</small>
              <small>
                {t('expires')}: {formatDateTime(cloudSyncAccount.sessionExpiresAt)}
              </small>
              {cloudSyncAccount.lastVerifiedAt ? (
                <small>
                  {t('lastVerified')}: {formatDateTime(cloudSyncAccount.lastVerifiedAt)}
                </small>
              ) : null}
            </div>
          </div>
          <button
            className="secondary-action logout-button"
            disabled={isSubmitting}
            onClick={() => void disconnect()}
            type="button"
          >
            <Icon name="logout" />
            {t('disconnectCloudAccount')}
          </button>
        </div>
      ) : (
        <form className="auth-form" onSubmit={(event) => void submit(event)}>
          <div className="auth-method-card" data-state={cloudApiBaseUrl ? 'ready' : undefined}>
            <Icon name="cloud" />
            <div>
              <strong>{t('cloudServerUrl')}</strong>
              <p>{cloudApiBaseUrl || t('cloudServerUrlNotConfigured')}</p>
            </div>
          </div>
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
          <label>
            {t('password')}
            <input
              autoComplete="current-password"
              minLength={8}
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </label>
          <button
            className="auth-submit-button"
            disabled={isSubmitting || !cloudApiBaseUrl || !email.trim() || password.length < 8}
            type="submit"
          >
            <Icon name="login" />
            {t('connectCloudAccount')}
          </button>
        </form>
      )}

      {formError ? <p className="form-error">{formError}</p> : null}
    </section>
  );
}
function normalizeSignupMode(
  signupMode: AuthClientOptionsResponse['signupMode'],
): 'Open' | 'Whitelist' | 'InviteOnly' | 'Closed' {
  switch (signupMode) {
    case 2:
    case 'Whitelist':
      return 'Whitelist';
    case 3:
    case 'InviteOnly':
      return 'InviteOnly';
    case 4:
    case 'Closed':
      return 'Closed';
    case 1:
    case 'Open':
    default:
      return 'Open';
  }
}

function formatSessionType(
  sessionType: AuthSessionListItemResponse['sessionType'],
  t: Translate,
) {
  switch (sessionType) {
    case 'DesktopLocal':
    case 2:
      return t('sessionDesktopLocal');
    case 'DesktopCloud':
    case 3:
      return t('sessionDesktopCloud');
    case 'Development':
    case 4:
      return t('sessionDevelopment');
    case 'Guest':
    case 5:
      return t('sessionGuest');
    case 'Browser':
    case 1:
    default:
      return t('sessionBrowser');
  }
}

function sessionIcon(sessionType: AuthSessionListItemResponse['sessionType']): IconName {
  switch (sessionType) {
    case 'DesktopLocal':
    case 2:
      return 'panel';
    case 'DesktopCloud':
    case 3:
      return 'cloud';
    case 'Development':
    case 4:
      return 'shield';
    case 'Guest':
    case 5:
      return 'user';
    case 'Browser':
    case 1:
    default:
      return 'login';
  }
}

export function SettingsPanel({
  cleanupCloudLinkedWorkspaceIds,
  cleanupPreferredWorkspaceId,
  cleanupWorkspaces,
  configuredStatuses,
  language,
  onChangeLanguage,
  onClose,
  onDeleteOldArchivedTasks,
  onDeleteWorkspace,
  onSaveStatusOptions,
  t,
}: {
  cleanupCloudLinkedWorkspaceIds: string[];
  cleanupPreferredWorkspaceId: string | null;
  cleanupWorkspaces: WorkspaceResponse[];
  configuredStatuses: StatusOption[];
  language: Language;
  onChangeLanguage: (language: Language) => void;
  onClose: () => void;
  onDeleteOldArchivedTasks: (
    workspaceId: string,
    olderThanDays: number,
    status?: string | null,
  ) => Promise<number>;
  onDeleteWorkspace: ((workspaceId: string) => Promise<void>) | null;
  onSaveStatusOptions: (statuses: StatusOption[]) => void;
  t: Translate;
}) {
  const [activeSection, setActiveSection] = useState<SettingsSectionKey>('general');
  const [statusDraft, setStatusDraft] = useState('');
  const [statusColorDraft, setStatusColorDraft] = useState('#d7dee8');
  const [workspacePendingDeletion, setWorkspacePendingDeletion] =
    useState<WorkspaceResponse | null>(null);
  const [archiveCleanupMode, setArchiveCleanupMode] =
    useState<'all' | 'older' | 'status' | null>(null);
  const [archiveCleanupDays, setArchiveCleanupDays] = useState(30);
  const [archiveCleanupStatus, setArchiveCleanupStatus] = useState(
    configuredStatuses[0]?.name ?? '',
  );
  const [cleanupWorkspaceId, setCleanupWorkspaceId] = useState(
    cleanupWorkspaces.some((workspace) => workspace.id === cleanupPreferredWorkspaceId)
      ? cleanupPreferredWorkspaceId ?? ''
      : cleanupWorkspaces[0]?.id ?? '',
  );
  const cleanupWorkspace = cleanupWorkspaces.find(
    (workspace) => workspace.id === cleanupWorkspaceId,
  ) ?? null;
  const cleanupIsCloudLinked = cleanupWorkspace
    ? cleanupCloudLinkedWorkspaceIds.includes(cleanupWorkspace.id)
    : false;

  useEffect(() => {
    if (cleanupWorkspaces.some((workspace) => workspace.id === cleanupWorkspaceId)) {
      return;
    }

    setCleanupWorkspaceId(
      cleanupWorkspaces.some((workspace) => workspace.id === cleanupPreferredWorkspaceId)
        ? cleanupPreferredWorkspaceId ?? ''
        : cleanupWorkspaces[0]?.id ?? '',
    );
  }, [cleanupPreferredWorkspaceId, cleanupWorkspaceId, cleanupWorkspaces]);
  const settingsSections: Array<{ key: SettingsSectionKey; label: string; icon: IconName }> = [
    { key: 'general', label: t('settingsGeneral'), icon: 'settings' },
    { key: 'statuses', label: t('statusOptions'), icon: 'status' },
    { key: 'cleanup', label: t('cleanup'), icon: 'trash' },
  ];

  const addStatus = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedStatus = statusDraft.trim();

    if (!trimmedStatus) {
      return;
    }

    onSaveStatusOptions([...configuredStatuses, { name: trimmedStatus, color: statusColorDraft }]);
    setStatusDraft('');
  };

  return (
    <>
      <ModalFrame onClose={onClose}>
        <section
          aria-labelledby="settings-title"
          aria-modal="true"
          className="settings-panel"
          role="dialog"
        >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">DumpTether</p>
            <h2 id="settings-title">{t('settings')}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>

        <div className="settings-layout">
          <nav className="settings-menu" aria-label={t('settingsSections')}>
            {settingsSections.map((section) => (
              <button
                aria-current={activeSection === section.key ? 'page' : undefined}
                key={section.key}
                onClick={() => setActiveSection(section.key)}
                type="button"
              >
                <Icon name={section.icon} />
                {section.label}
              </button>
            ))}
          </nav>

          <div className="settings-content">
            {activeSection === 'general' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('settingsGeneral')}</h3>
                <label>
                  {t('language')}
                  <select
                    onChange={(event) => onChangeLanguage(event.target.value as Language)}
                    value={language}
                  >
                    <option value="en">{t('english')}</option>
                    <option value="da">{t('danish')}</option>
                  </select>
                </label>
              </div>
            ) : null}

            {activeSection === 'statuses' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('statusOptions')}</h3>
                <form className="settings-inline-form" onSubmit={addStatus}>
                  <input
                    aria-label={t('addStatus')}
                    onChange={(event) => setStatusDraft(event.target.value)}
                    placeholder={t('addStatus')}
                    type="text"
                    value={statusDraft}
                  />
                  <input
                    aria-label={t('statusColor')}
                    onChange={(event) => setStatusColorDraft(event.target.value)}
                    type="color"
                    value={statusColorDraft}
                  />                  <button className="icon-button" disabled={!statusDraft.trim()} type="submit">
                    <Icon name="plus" />
                  </button>
                </form>
                <div className="settings-chip-list">
                  {configuredStatuses.map((status) => (
                    <span className="settings-chip settings-status-chip" key={status.name}>
                      <input
                        aria-label={`${t('statusColor')}: ${status.name}`}
                        onChange={(event) => onSaveStatusOptions(configuredStatuses.map((candidate) =>
                          candidate.name === status.name
                            ? { ...candidate, color: event.target.value }
                            : candidate))}
                        type="color"
                        value={status.color}
                      />
                      {status.name}
                      <button
                        className="tiny-icon-button"
                        onClick={() =>
                          onSaveStatusOptions(
                            configuredStatuses.filter((currentStatus) => currentStatus.name !== status.name),
                          )}
                        title={t('deleteNote')}
                        type="button"
                      >
                        <Icon name="trash" />
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            ) : null}

            {activeSection === 'cleanup' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('cleanup')}</h3>
                <p>{t('cleanupFuture')}</p>
                {cleanupWorkspaces.length > 0 ? (
                  <label className="field-label">
                    {t('cleanupBoard')}
                    <select
                      onChange={(event) => setCleanupWorkspaceId(event.target.value)}
                      value={cleanupWorkspaceId}
                    >
                      {cleanupWorkspaces.map((cleanupCandidate) => (
                        <option key={cleanupCandidate.id} value={cleanupCandidate.id}>
                          {cleanupCandidate.name}
                        </option>
                      ))}
                    </select>
                  </label>
                ) : (
                  <p className="form-help">{t('cleanupNeedsOwnedBoard')}</p>
                )}
                {cleanupIsCloudLinked ? (
                  <p className="form-help">{t('cleanupCloudLinkedUnavailable')}</p>
                ) : null}
                <div className="settings-action-grid">
                  <button
                    disabled={!cleanupWorkspace || cleanupIsCloudLinked}
                    onClick={() => setArchiveCleanupMode('all')}
                    type="button"
                  >
                    <Icon name="trash" />
                    {t('clearArchive')}
                  </button>
                  <button
                    disabled={!cleanupWorkspace || cleanupIsCloudLinked}
                    onClick={() => setArchiveCleanupMode('older')}
                    type="button"
                  >
                    <Icon name="clock" />
                    {t('clearOldTasks')}
                  </button>
                  <button
                    disabled={
                      !cleanupWorkspace ||
                      cleanupIsCloudLinked ||
                      configuredStatuses.length === 0
                    }
                    onClick={() => setArchiveCleanupMode('status')}
                    type="button"
                  >
                    <Icon name="status" />
                    {t('clearTasksWithStatus')}
                  </button>
                  <button
                    className="danger-action"
                    disabled={!cleanupWorkspace || !onDeleteWorkspace}
                    onClick={() => setWorkspacePendingDeletion(cleanupWorkspace)}
                    type="button"
                  >
                    <Icon name="trash" />
                    {cleanupWorkspace
                      ? `${t('deleteBoard')}: ${cleanupWorkspace.name}`
                      : t('deleteBoard')}
                  </button>
                </div>
              </div>
            ) : null}
          </div>
        </div>
        </section>
      </ModalFrame>
      {workspacePendingDeletion && onDeleteWorkspace ? (
        <DeleteWorkspaceDialog
          onClose={() => setWorkspacePendingDeletion(null)}
          onDelete={async () => {
            await onDeleteWorkspace(workspacePendingDeletion.id);
            setWorkspacePendingDeletion(null);
            onClose();
          }}
          t={t}
          workspace={workspacePendingDeletion}
        />
      ) : null}
      {archiveCleanupMode && cleanupWorkspace ? (
        <ArchiveCleanupDialog
          days={archiveCleanupDays}
          mode={archiveCleanupMode}
          onChangeDays={setArchiveCleanupDays}
          onChangeStatus={setArchiveCleanupStatus}
          onClose={() => setArchiveCleanupMode(null)}
          onDelete={async () => {
            await onDeleteOldArchivedTasks(
              cleanupWorkspace.id,
              archiveCleanupMode === 'older' ? archiveCleanupDays : 0,
              archiveCleanupMode === 'status' ? archiveCleanupStatus : null,
            );
            setArchiveCleanupMode(null);
          }}
          t={t}
          status={archiveCleanupStatus}
          statuses={configuredStatuses.map((status) => status.name)}
          workspace={cleanupWorkspace}
        />
      ) : null}
    </>
  );
}

function ArchiveCleanupDialog({
  days,
  mode,
  onChangeDays,
  onChangeStatus,
  onClose,
  onDelete,
  t,
  status,
  statuses,
  workspace,
}: {
  days: number;
  mode: 'all' | 'older' | 'status';
  onChangeDays: (days: number) => void;
  onChangeStatus: (status: string) => void;
  onClose: () => void;
  onDelete: () => Promise<void>;
  t: Translate;
  status: string;
  statuses: string[];
  workspace: WorkspaceResponse;
}) {
  const [isDeleting, setIsDeleting] = useState(false);

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="archive-cleanup-title"
        aria-modal="true"
        className="delete-workspace-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('cleanup')}</p>
            <h2 id="archive-cleanup-title">{workspace.name}</h2>
          </div>
          <button className="icon-button" disabled={isDeleting} onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>
        <p>{t('cleanupArchiveWarning')}</p>
        {mode === 'older' ? (
          <label className="field-label">
            {t('olderThanDays')}
            <input
              min={1}
              onChange={(event) => onChangeDays(Math.max(1, Number(event.target.value)))}
              type="number"
              value={days}
            />
          </label>
        ) : null}
        {mode === 'status' ? (
          <label className="field-label">
            {t('status')}
            <select
              onChange={(event) => onChangeStatus(event.target.value)}
              value={status}
            >
              {statuses.map((statusOption) => (
                <option key={statusOption} value={statusOption}>
                  {statusOption}
                </option>
              ))}
            </select>
          </label>
        ) : null}
        <div className="dialog-actions">
          <button className="ghost-button" disabled={isDeleting} onClick={onClose} type="button">
            {t('cancel')}
          </button>
          <button
            className="danger-action"
            disabled={
              isDeleting ||
              (mode === 'older' && days < 1) ||
              (mode === 'status' && !status)
            }
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
            {t('deleteArchivedNow')}
          </button>
        </div>
      </section>
    </ModalFrame>
  );
}
