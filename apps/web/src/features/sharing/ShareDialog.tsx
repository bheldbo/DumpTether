import { FormEvent, useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import {
  buildShareUrl,
  copyTextToClipboard,
  formatDateTime,
  formatTaskShareRole,
  formatWorkspaceRole,
  getErrorMessage,
  isOwnerRole,
  isReadOnlyRole,
  isReadOnlyTaskShareRole,
} from '../../appUtils';
import { type Translate } from '../../localization';
import type {
  CreateTaskShareRequest,
  TaskItemDetailResponse,
  TaskItemShareResponse,
  TaskItemShareRole,
  TaskShareLinkResponse,
  UpdateTaskShareRequest,
  UpdateWorkspaceMemberRequest,
  WorkspaceInvitationResponse,
  WorkspaceMemberResponse,
  WorkspaceMembershipRole,
} from '../../types';

export function PendingInvitationChip({
  invitation,
  onRevoke,
  t,
}: {
  invitation: WorkspaceInvitationResponse;
  onRevoke: () => Promise<void>;
  t: Translate;
}) {
  return (
    <span className="pending-invite-chip pending-invite-chip-inline" title={invitation.email}>
      <Icon name="mail" />
      <span>{invitation.email}</span>
      <small>{t('pendingInvites')}</small>
      <button
        className="tiny-icon-button"
        onClick={() => void onRevoke()}
        title={t('revokeInvite')}
        type="button"
      >
        <Icon name="close" />
      </button>
    </span>
  );
}

export function WorkspaceMemberChip({
  isConfirming,
  member,
  onCancelRemove,
  onConfirmRemove,
  onOpenManage,
  onRequestRemove,
  t,
}: {
  isConfirming: boolean;
  member: WorkspaceMemberResponse;
  onCancelRemove: () => void;
  onConfirmRemove: () => Promise<void>;
  onOpenManage: () => void;
  onRequestRemove: () => void;
  t: Translate;
}) {
  const canRemove = !isOwnerRole(member.role);
  const isOwner = isOwnerRole(member.role);

  return (
    <span
      className={`member-chip member-chip-manageable${isOwner ? ' member-chip-owner' : ''}`}
      data-confirming={isConfirming}
      onClick={(event) => {
        if (event.target instanceof HTMLElement && event.target.closest('.member-chip-remove, .member-chip-confirm')) {
          return;
        }

        onOpenManage();
      }}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onOpenManage();
        }
      }}
      role={canRemove ? 'button' : undefined}
      tabIndex={canRemove ? 0 : undefined}
      title={isOwner ? `${member.email} - ${t('roleOwner')}` : member.email}
    >
      <Icon name={isOwner ? 'crown' : 'user'} />
      <span>{member.displayName || member.email}</span>
      {canRemove ? (
        isConfirming ? (
          <span className="member-chip-confirm">
            <button
              className="tiny-icon-button"
              onClick={() => void onConfirmRemove()}
              title={t('removeMember')}
              type="button"
            >
              <Icon name="check" />
            </button>
            <button
              className="tiny-icon-button"
              onClick={onCancelRemove}
              title={t('cancel')}
              type="button"
            >
              <Icon name="close" />
            </button>
          </span>
        ) : (
          <button
            className="tiny-icon-button member-chip-remove"
            onClick={onRequestRemove}
            title={t('removeMember')}
            type="button"
          >
            <Icon name="close" />
          </button>
        )
      ) : null}
    </span>
  );
}

export function ShareDialog({
  existingTaskShares,
  focusedTaskShareId = null,
  focusedWorkspaceMemberId = null,
  onClose,
  onCreate,
  onRemoveWorkspaceMember,
  onRevokeTaskShare,
  onRevokeWorkspaceInvitation,
  onUpdateTaskShareRole,
  onUpdateWorkspaceMemberRole,
  pendingInvitations,
  roleMode,
  t,
  title,
  workspaceMembers = [],
}: {
  existingTaskShares: TaskItemShareResponse[];
  focusedTaskShareId?: string | null;
  focusedWorkspaceMemberId?: string | null;
  onClose: () => void;
  onCreate: (
    email: string,
    role: string,
  ) => Promise<{ token: string | null; expiresAt: string }>;
  onRemoveWorkspaceMember?: (userId: string) => Promise<void>;
  onRevokeTaskShare?: (shareId: string) => Promise<void>;
  onRevokeWorkspaceInvitation?: (id: string) => Promise<void>;
  onUpdateTaskShareRole?: (
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => Promise<unknown>;
  onUpdateWorkspaceMemberRole?: (
    userId: string,
    requestBody: UpdateWorkspaceMemberRequest,
  ) => Promise<unknown>;
  pendingInvitations: WorkspaceInvitationResponse[];
  roleMode: 'task' | 'workspace';
  t: Translate;
  title: string;
  workspaceMembers?: WorkspaceMemberResponse[];
}) {
  const [shareEmail, setShareEmail] = useState('');
  const [shareRole, setShareRole] = useState('Member');
  const [createdLink, setCreatedLink] = useState<string | null>(null);
  const [copiedText, setCopiedText] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const visibleTaskShares = existingTaskShares.filter((share) => !share.revokedAt);
  const visibleWorkspaceMembers = workspaceMembers;

  const copyShareUrl = async (value: string) => {
    await copyTextToClipboard(value);
    setCopiedText(true);
  };

  const submitShare = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedEmail = shareEmail.trim();

    if (!trimmedEmail) {
      return;
    }

    setError(null);
    setIsSubmitting(true);

    try {
      const created = await onCreate(
        trimmedEmail,
        roleMode === 'task'
          ? shareRole === 'ReadOnly' ? 'Viewer' : 'Editor'
          : shareRole);
      if (created.token) {
        setCreatedLink(buildShareUrl(created.token));
      }
      setShareEmail('');
      setCopiedText(false);
    } catch (shareError) {
      setError(getErrorMessage(shareError));
    } finally {
      setIsSubmitting(false);
    }
  };

  const updateWorkspaceMemberRole = async (
    userId: string,
    role: WorkspaceMembershipRole,
  ) => {
    if (!onUpdateWorkspaceMemberRole) {
      return;
    }

    setError(null);
    try {
      await onUpdateWorkspaceMemberRole(userId, { role });
    } catch (updateError) {
      setError(getErrorMessage(updateError));
    }
  };

  const updateTaskShareRole = async (
    shareId: string,
    role: 'Member' | 'ReadOnly',
  ) => {
    if (!onUpdateTaskShareRole) {
      return;
    }

    const nextRole = role === 'ReadOnly' ? 'Viewer' : 'Editor';
    setError(null);
    try {
      await onUpdateTaskShareRole(shareId, { role: nextRole as TaskItemShareRole });
    } catch (updateError) {
      setError(getErrorMessage(updateError));
    }
  };

  return (
    <ModalFrame className="dialog-backdrop share-dialog-backdrop" onClose={onClose}>
      <section
        aria-labelledby="share-dialog-title"
        aria-modal="true"
        className="workspace-invite-dialog share-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('sharing')}</p>
            <h2 id="share-dialog-title">{title}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>

        <form className="workspace-invite-form share-dialog-form" onSubmit={(event) => void submitShare(event)}>
          <input
            aria-label={t('inviteEmail')}
            autoFocus
            onChange={(event) => setShareEmail(event.target.value)}
            placeholder={t('inviteEmail')}
            type="email"
            value={shareEmail}
          />
          <select
            aria-label={t('shareRole')}
            className="share-role-select"
            onChange={(event) => setShareRole(event.target.value)}
            value={shareRole}
          >
            <option value="Member">{t('roleMember')}</option>
            <option value="ReadOnly">{t('roleReadOnly')}</option>
          </select>
          <button className="icon-button" disabled={!shareEmail.trim() || isSubmitting} type="submit">
            <Icon name="check" />
          </button>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
          </button>
        </form>

        {createdLink ? (
          <div className="invite-token-panel">
            <small>{t('shareLinkHelp')}</small>
            <button
              className="invite-token"
              onClick={() => void copyShareUrl(createdLink)}
              type="button"
            >
              {createdLink}
            </button>
            {copiedText ? (
              <small className="copied-feedback">{t('copiedToClipboard')}</small>
            ) : null}
          </div>
        ) : null}

        {error ? <p className="form-error">{error}</p> : null}

        {visibleWorkspaceMembers.length > 0 ||
        pendingInvitations.length > 0 ||
        visibleTaskShares.length > 0 ? (
          <div className="pending-invite-list share-dialog-list">
            {visibleWorkspaceMembers.map((member) => {
              const isOwner = isOwnerRole(member.role);

              return (
                <div
                  className="share-person-row"
                  data-focused={focusedWorkspaceMemberId === member.userId}
                  key={member.userId}
                >
                  <Icon name={isOwner ? 'crown' : 'user'} />
                  <span className="share-person-copy">
                    <strong>{member.displayName || member.email}</strong>
                    <small>{member.email}</small>
                  </span>
                  <select
                    aria-label={t('shareRole')}
                    className="share-role-select"
                    disabled={isOwner || !onUpdateWorkspaceMemberRole}
                    onChange={(event) =>
                      void updateWorkspaceMemberRole(
                        member.userId,
                        event.target.value as WorkspaceMembershipRole,
                      )}
                    value={isReadOnlyRole(member.role) ? 'ReadOnly' : isOwner ? 'Owner' : 'Member'}
                  >
                    {isOwner ? <option value="Owner">{t('roleOwner')}</option> : null}
                    <option value="Member">{t('roleMember')}</option>
                    <option value="ReadOnly">{t('roleReadOnly')}</option>
                  </select>
                  {!isOwner && onRemoveWorkspaceMember ? (
                    <button
                      className="tiny-icon-button"
                      onClick={() => void onRemoveWorkspaceMember(member.userId)}
                      title={t('removeMember')}
                      type="button"
                    >
                      <Icon name="close" />
                    </button>
                  ) : null}
                </div>
              );
            })}
            {pendingInvitations.map((invitation) => (
              <span
                className="pending-invite-chip"
                key={invitation.id}
                title={`${invitation.email} - ${formatDateTime(invitation.expiresAt)}`}
              >
                <Icon name="mail" />
                <span>{invitation.email}</span>
                <small>{formatWorkspaceRole(invitation.role, t)} - {t('pendingInvites')}</small>
                {onRevokeWorkspaceInvitation ? (
                  <button
                    className="tiny-icon-button"
                    onClick={() => void onRevokeWorkspaceInvitation(invitation.id)}
                    title={t('revokeInvite')}
                    type="button"
                  >
                    <Icon name="close" />
                  </button>
                ) : null}
              </span>
            ))}
            {visibleTaskShares.map((share) => (
              <div
                className="share-person-row"
                data-focused={focusedTaskShareId === share.id}
                key={share.id}
                title={`${share.email}${share.expiresAt ? ` - ${formatDateTime(share.expiresAt)}` : ''}`}
              >
                <Icon name={share.acceptedAt ? 'user' : 'mail'} />
                <span className="share-person-copy">
                  <strong>{share.email}</strong>
                  <small>{share.acceptedAt ? t('sharedWith') : t('pendingInvites')}</small>
                </span>
                <select
                  aria-label={t('shareRole')}
                  className="share-role-select"
                  disabled={!onUpdateTaskShareRole}
                  onChange={(event) =>
                    void updateTaskShareRole(
                      share.id,
                      event.target.value as 'Member' | 'ReadOnly',
                    )}
                  value={isReadOnlyTaskShareRole(share.role) ? 'ReadOnly' : 'Member'}
                >
                  <option value="Member">{t('roleMember')}</option>
                  <option value="ReadOnly">{t('roleReadOnly')}</option>
                </select>
                {onRevokeTaskShare ? (
                  <button
                    className="tiny-icon-button"
                    onClick={() => void onRevokeTaskShare(share.id)}
                    title={t('removeShare')}
                    type="button"
                  >
                    <Icon name="close" />
                  </button>
                ) : null}
              </div>
            ))}
          </div>
        ) : (
          <p className="context-muted share-dialog-empty">{t('notShared')}</p>
        )}
      </section>
    </ModalFrame>
  );
}

export function TaskShareStrip({
  onCreateTaskShareLink,
  onRevokeTaskShare,
  onUpdateTaskShareRole,
  t,
  taskItem,
}: {
  onCreateTaskShareLink: (
    taskItemId: string,
    requestBody: CreateTaskShareRequest,
  ) => Promise<TaskShareLinkResponse>;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onUpdateTaskShareRole: (
    taskItemId: string,
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => Promise<TaskItemDetailResponse>;
  t: Translate;
  taskItem: TaskItemDetailResponse;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [focusedTaskShareId, setFocusedTaskShareId] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const visibleShares = taskItem.shares.filter((share) => !share.revokedAt);

  return (
    <div
      className="task-share-popover"
      onClick={(event) => event.stopPropagation()}
      onPointerDown={(event) => event.stopPropagation()}
      ref={menuRef}
    >
      <div className="member-chip-strip task-share-strip" aria-label={t('sharing')}>
        {visibleShares.slice(0, 3).map((share) => (
          <button
            className="member-chip share-person-chip"
            key={share.id}
            onClick={(event) => {
              event.stopPropagation();
              setFocusedTaskShareId(share.id);
              setIsOpen(true);
            }}
            title={`${share.email} - ${formatTaskShareRole(share.role, t)}`}
            type="button"
          >
            <Icon name={share.acceptedAt ? 'user' : 'mail'} />
            <span>{share.email}</span>
          </button>
        ))}
        {visibleShares.length > 3 ? (
          <button
            className="member-chip"
            onClick={(event) => {
              event.stopPropagation();
              setFocusedTaskShareId(null);
              setIsOpen(true);
            }}
            type="button"
          >
            +{visibleShares.length - 3}
          </button>
        ) : null}
        <button
          className="tiny-icon-button task-share-trigger"
          onClick={(event) => {
            event.stopPropagation();
            setFocusedTaskShareId(null);
            setIsOpen((open) => !open);
          }}
          title={t('shareTask')}
          type="button"
        >
          <Icon name="userPlus" />
        </button>
      </div>

      {isOpen ? (
        <ShareDialog
          existingTaskShares={taskItem.shares}
          focusedTaskShareId={focusedTaskShareId}
          onClose={() => setIsOpen(false)}
          onCreate={async (email, role) =>
            await onCreateTaskShareLink(taskItem.id, {
              email,
              role: role as TaskItemShareRole,
            })}
          onRevokeTaskShare={(shareId) => onRevokeTaskShare(taskItem.id, shareId)}
          onUpdateTaskShareRole={(shareId, requestBody) =>
            onUpdateTaskShareRole(taskItem.id, shareId, requestBody)}
          pendingInvitations={[]}
          roleMode="task"
          t={t}
          title={taskItem.title}
        />
      ) : null}
    </div>
  );
}
