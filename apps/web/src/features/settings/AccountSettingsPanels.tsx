import { type FormEvent, useEffect, useState } from 'react';
import { beginOAuthLogin } from '../../api';
import type { SettingsSectionKey } from '../../appTypes';
import { readCloudSyncApiBaseUrl } from '../../appSettings';
import { Icon, type IconName } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import {
  formatDateTime,
  formatOAuthProvider,
  formatRelativeDate,
  formatWorkspaceRole,
  getErrorMessage,
} from '../../appUtils';
import type { Language, Translate } from '../../localization';
import type {
  ArchiveResolutionResponse,
  AuthSessionListItemResponse,
  AuthClientOptionsResponse,
  CloudSyncAccountResponse,
  ConnectCloudAccountRequest,
  CreateArchiveResolutionRequest,
  CurrentUserResponse,
  LoginUserRequest,
  RegisterUserRequest,
  RegisterUserResponse,
  TaskShareInboxResponse,
  UpdateArchiveResolutionRequest,
  WorkspaceInvitationInboxResponse,
} from '../../types';

export function AuthPanel({
  authOptions,
  currentUser,
  isLoading,
  localDesktopSessionIsActive,
  onDevelopmentLogin,
  onGuestLogin,
  onLogin,
  onLogout,
  onRegister,
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
  onLogin: (requestBody: LoginUserRequest) => Promise<void>;
  onLogout?: () => Promise<void>;
  onRegister: (requestBody: RegisterUserRequest) => Promise<RegisterUserResponse>;
  temporarySessionIsActive: boolean;
  t: Translate;
  variant: 'gate' | 'settings';
}) {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [inviteCode, setInviteCode] = useState('');
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const signupMode = normalizeSignupMode(authOptions.signupMode);
  const registrationIsAvailable = signupMode !== 'Closed';
  const registrationNeedsInvite = signupMode === 'InviteOnly';

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
        });
        setStatusMessage(
          registered.emailConfirmationRequired
            ? t('emailConfirmationSent')
            : t('authRegistered'),
        );
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

  const wrapperClassName = variant === 'gate'
    ? 'auth-gate'
    : 'settings-section auth-panel';
  const canSubmit = email.trim().length > 0 &&
    password.length >= 8 &&
    (mode !== 'register' || registrationIsAvailable) &&
    (!registrationNeedsInvite || inviteCode.trim().length > 0);

  if (currentUser) {
    return (
      <section className={wrapperClassName} aria-label={t('account')}>
        <div className="auth-heading">
          <p className="detail-kicker">{t('account')}</p>
          <h2>{currentUser.user.displayName || currentUser.user.email}</h2>
          <p>{t('signedInAs')}: {currentUser.user.email}</p>
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
        {onLogout ? (
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
        {formError ? <p className="form-error">{formError}</p> : null}
      </section>
    );
  }

  return (
    <section className={wrapperClassName} aria-label={t('account')}>
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

      {authOptions.oAuthProviders.length > 0 ? (
        <div className="oauth-login-list">
          {authOptions.oAuthProviders.map((provider) => (
            <button
              className="secondary-action"
              disabled={isSubmitting || isLoading}
              key={provider}
              onClick={() => beginOAuthLogin(provider)}
              type="button"
            >
              {formatOAuthProvider(provider, t)}
            </button>
          ))}
        </div>
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

        <button
          className="auth-submit-button"
          disabled={!canSubmit || isSubmitting || isLoading}
          type="submit"
        >
          <Icon name={mode === 'register' ? 'user' : 'login'} />
          {mode === 'register' ? t('registerButton') : t('loginButton')}
        </button>
      </form>

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
    </section>
  );
}

export function AccountPanel({
  authSessions,
  authOptions,
  cloudSyncAccount,
  currentUser,
  incomingTaskShares,
  incomingWorkspaceInvitations,
  isLoadingAuth,
  localDesktopSessionIsActive,
  onAcceptIncomingWorkspaceInvitation,
  onConnectCloudAccount,
  onClose,
  onDeclineIncomingWorkspaceInvitation,
  onDisconnectCloudAccount,
  onDevelopmentLogin,
  onGuestLogin,
  onLeaveTaskShare,
  onLogin,
  onLogout,
  onRegister,
  onRevokeAuthSession,
  temporarySessionIsActive,
  t,
}: {
  authSessions: AuthSessionListItemResponse[];
  authOptions: AuthClientOptionsResponse;
  cloudSyncAccount: CloudSyncAccountResponse | null;
  currentUser: CurrentUserResponse | null;
  incomingTaskShares: TaskShareInboxResponse[];
  incomingWorkspaceInvitations: WorkspaceInvitationInboxResponse[];
  isLoadingAuth: boolean;
  localDesktopSessionIsActive: boolean;
  onAcceptIncomingWorkspaceInvitation: (id: string) => Promise<void>;
  onConnectCloudAccount: (requestBody: ConnectCloudAccountRequest) => Promise<void>;
  onClose: () => void;
  onDeclineIncomingWorkspaceInvitation: (id: string) => Promise<void>;
  onDisconnectCloudAccount: () => Promise<void>;
  onDevelopmentLogin: () => Promise<void>;
  onGuestLogin: () => Promise<void>;
  onLeaveTaskShare: (shareId: string) => Promise<void>;
  onLogin: (requestBody: LoginUserRequest) => Promise<void>;
  onLogout: () => Promise<void>;
  onRegister: (requestBody: RegisterUserRequest) => Promise<RegisterUserResponse>;
  onRevokeAuthSession: (sessionId: string) => Promise<void>;
  temporarySessionIsActive: boolean;
  t: Translate;
}) {
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
          onGuestLogin={onGuestLogin}
          onLogin={onLogin}
          onLogout={onLogout}
          onRegister={onRegister}
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

        {currentUser ? (
          <section className="settings-section">
            <h3>{t('sessions')}</h3>
            {authSessions.length === 0 ? (
              <p>{t('noSessions')}</p>
            ) : (
              <div className="account-notification-list">
                {authSessions.map((session) => {
                  const isRevoked = Boolean(session.revokedAt);
                  return (
                    <article className="account-notification-card" key={session.id}>
                      <Icon name={sessionIcon(session.sessionType)} />
                      <div>
                        <strong>
                          {formatSessionType(session.sessionType, t)}
                          {session.isCurrent ? ` (${t('currentSession')})` : ''}
                        </strong>
                        <p>
                          {session.deviceName || t('unknownDevice')}
                          {' - '}
                          {t('lastSeen')}: {formatRelativeDate(session.lastSeenAt)}
                        </p>
                        <small title={formatDateTime(session.createdAt)}>
                          {t('created')}: {formatDateTime(session.createdAt)}
                        </small>
                        {isRevoked ? (
                          <small>{t('revoked')}: {formatDateTime(session.revokedAt!)}</small>
                        ) : (
                          <small>{t('expires')}: {formatDateTime(session.expiresAt)}</small>
                        )}
                      </div>
                      {!isRevoked ? (
                        <button
                          className="secondary-action logout-button"
                          onClick={() => void onRevokeAuthSession(session.id)}
                          type="button"
                        >
                          <Icon name="logout" />
                          {session.isCurrent ? t('logout') : t('revokeSession')}
                        </button>
                      ) : null}
                    </article>
                  );
                })}
              </div>
            )}
          </section>
        ) : null}

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

        <section className="settings-section">
          <h3>{t('signInMethods')}</h3>
          <div className="auth-method-list">
            <div className="auth-method-card" data-state="ready">
              <Icon name="mail" />
              <div>
                <strong>{t('emailPasswordLogin')}</strong>
                <p>{t('emailPasswordLoginHelp')}</p>
              </div>
            </div>
            <div className="auth-method-card">
              <Icon name="cloud" />
              <div>
                <strong>{t('oauthLogin')}</strong>
                <p>{t('oauthLoginHelp')}</p>
              </div>
              <span>{t('configRequired')}</span>
            </div>
            <div className="auth-method-card">
              <Icon name="shield" />
              <div>
                <strong>{t('emailMfa')}</strong>
                <p>{t('emailMfaHelp')}</p>
              </div>
              <span>{t('configRequired')}</span>
            </div>
          </div>
        </section>
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
        <div className="account-notification-card">
          <Icon name="cloud" />
          <div>
            <strong>{cloudSyncAccount.cloudDisplayName || cloudSyncAccount.cloudEmail}</strong>
            <p>{cloudSyncAccount.cloudEmail}</p>
            <small>{cloudSyncAccount.cloudApiBaseUrl}</small>
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
  archiveResolutions,
  configuredStatuses,
  language,
  onChangeLanguage,
  onClose,
  onCreateArchiveResolution,
  onDeleteArchiveResolution,
  onSaveStatusOptions,
  onUpdateArchiveResolution,
  t,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  configuredStatuses: string[];
  language: Language;
  onChangeLanguage: (language: Language) => void;
  onClose: () => void;
  onCreateArchiveResolution: (requestBody: CreateArchiveResolutionRequest) => Promise<void>;
  onDeleteArchiveResolution: (id: string) => Promise<void>;
  onSaveStatusOptions: (statuses: string[]) => void;
  onUpdateArchiveResolution: (
    id: string,
    requestBody: UpdateArchiveResolutionRequest,
  ) => Promise<void>;
  t: Translate;
}) {
  const [activeSection, setActiveSection] = useState<SettingsSectionKey>('general');
  const [statusDraft, setStatusDraft] = useState('');
  const [archiveReasonName, setArchiveReasonName] = useState('');
  const [archiveReasonRequiresNote, setArchiveReasonRequiresNote] = useState(false);
  const settingsSections: Array<{ key: SettingsSectionKey; label: string; icon: IconName }> = [
    { key: 'general', label: t('settingsGeneral'), icon: 'settings' },
    { key: 'statuses', label: t('statusOptions'), icon: 'status' },
    { key: 'archive', label: t('archiveReasons'), icon: 'archive' },
    { key: 'cleanup', label: t('cleanup'), icon: 'trash' },
  ];

  const addStatus = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedStatus = statusDraft.trim();

    if (!trimmedStatus) {
      return;
    }

    onSaveStatusOptions([...configuredStatuses, trimmedStatus]);
    setStatusDraft('');
  };

  const addArchiveReason = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedName = archiveReasonName.trim();

    if (!trimmedName) {
      return;
    }

    await onCreateArchiveResolution({
      name: trimmedName,
      requiresExplanation: archiveReasonRequiresNote,
    });
    setArchiveReasonName('');
    setArchiveReasonRequiresNote(false);
  };

  return (
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
                  <button className="icon-button" disabled={!statusDraft.trim()} type="submit">
                    <Icon name="plus" />
                  </button>
                </form>
                <div className="settings-chip-list">
                  {configuredStatuses.map((status) => (
                    <span className="settings-chip" key={status}>
                      {status}
                      <button
                        className="tiny-icon-button"
                        onClick={() =>
                          onSaveStatusOptions(
                            configuredStatuses.filter((currentStatus) => currentStatus !== status),
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

            {activeSection === 'archive' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('archiveReasons')}</h3>
                <form className="settings-inline-form" onSubmit={(event) => void addArchiveReason(event)}>
                  <input
                    aria-label={t('addArchiveReason')}
                    onChange={(event) => setArchiveReasonName(event.target.value)}
                    placeholder={t('addArchiveReason')}
                    type="text"
                    value={archiveReasonName}
                  />
                  <label className="settings-checkbox">
                    <input
                      checked={archiveReasonRequiresNote}
                      onChange={(event) => setArchiveReasonRequiresNote(event.target.checked)}
                      type="checkbox"
                    />
                    {t('requireArchiveNote')}
                  </label>
                  <button className="icon-button" disabled={!archiveReasonName.trim()} type="submit">
                    <Icon name="plus" />
                  </button>
                </form>
                <div className="settings-list">
                  {archiveResolutions.map((reason) => (
                    <ArchiveResolutionSettingsRow
                      key={reason.id}
                      onDeleteArchiveResolution={onDeleteArchiveResolution}
                      onUpdateArchiveResolution={onUpdateArchiveResolution}
                      reason={reason}
                      t={t}
                    />
                  ))}
                </div>
              </div>
            ) : null}

            {activeSection === 'cleanup' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('cleanup')}</h3>
                <p>{t('cleanupFuture')}</p>
                <div className="settings-action-grid">
                  <button disabled type="button">{t('clearArchive')}</button>
                  <button disabled type="button">{t('clearOldTasks')}</button>
                  <button disabled type="button">{t('clearWorkspaceTasks')}</button>
                  <button disabled type="button">{t('deleteProjectTag')}</button>
                  <button disabled type="button">{t('deleteBoard')}</button>
                </div>
              </div>
            ) : null}
          </div>
        </div>
      </section>
    </ModalFrame>
  );
}

function ArchiveResolutionSettingsRow({
  onDeleteArchiveResolution,
  onUpdateArchiveResolution,
  reason,
  t,
}: {
  onDeleteArchiveResolution: (id: string) => Promise<void>;
  onUpdateArchiveResolution: (
    id: string,
    requestBody: UpdateArchiveResolutionRequest,
  ) => Promise<void>;
  reason: ArchiveResolutionResponse;
  t: Translate;
}) {
  const [name, setName] = useState(reason.name);
  const [requiresExplanation, setRequiresExplanation] = useState(reason.requiresExplanation);

  useEffect(() => {
    setName(reason.name);
    setRequiresExplanation(reason.requiresExplanation);
  }, [reason]);

  const saveReason = async () => {
    const trimmedName = name.trim();
    if (!trimmedName) {
      setName(reason.name);
      return;
    }

    await onUpdateArchiveResolution(reason.id, {
      name: trimmedName,
      requiresExplanation,
    });
  };

  return (
    <div className="settings-row">
      <input
        aria-label={reason.name}
        onBlur={() => void saveReason()}
        onChange={(event) => setName(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.currentTarget.blur();
          }
        }}
        type="text"
        value={name}
      />
      <label className="settings-checkbox">
        <input
          checked={requiresExplanation}
          onChange={(event) => {
            setRequiresExplanation(event.target.checked);
            void onUpdateArchiveResolution(reason.id, {
              name: name.trim() || reason.name,
              requiresExplanation: event.target.checked,
            });
          }}
          type="checkbox"
        />
        {t('requireArchiveNote')}
      </label>
      <button
        className="tiny-icon-button danger-icon-button"
        onClick={() => void onDeleteArchiveResolution(reason.id)}
        title={t('deleteNote')}
        type="button"
      >
        <Icon name="trash" />
      </button>
    </div>
  );
}
