import { type FormEvent, useEffect, useState } from 'react';
import { ColorPickerPopover } from '../../components/ColorPickerPopover';
import { Icon } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import {
  PendingInvitationChip,
  ShareDialog,
  WorkspaceMemberChip,
} from '../sharing/ShareDialog';
import { BoardSyncStatus } from '../sync/BoardSyncStatus';
import { SortMenu } from './SortMenu';
import {
  formatWorkspaceName,
  isOwnerRole,
} from '../../appUtils';
import { type Translate } from '../../localization';
import {
  getContextChipStyle,
  getWorkspaceHeaderStyle,
} from '../../taskUtils';
import type {
  CreateWorkspaceInvitationRequest,
  ProjectResponse,
  SavedViewSort,
  SyncRootResponse,
  UpdateProjectRequest,
  UpdateWorkspaceMemberRequest,
  UpdateWorkspaceRequest,
  WorkspaceInvitationResponse,
  WorkspaceMemberResponse,
  WorkspaceMembershipRole,
  WorkspaceResponse,
} from '../../types';

interface WorkspaceHeaderProps {
  canManageWorkspaceMetadata: boolean;
  canManageSharing: boolean;
  canSyncWorkspace: boolean;
  colorOptions: string[];
  invitations: WorkspaceInvitationResponse[];
  members: WorkspaceMemberResponse[];
  onCreateProject: (name: string, color?: string | null) => Promise<void>;
  onChangeSort: (sort: SavedViewSort) => void;
  onCreateWorkspaceInvitation: (
    requestBody: CreateWorkspaceInvitationRequest,
  ) => Promise<WorkspaceInvitationResponse>;
  onDeleteProject: (projectId: string) => Promise<void>;
  onRemoveWorkspaceMember: (userId: string) => Promise<void>;
  onRevokeWorkspaceInvitation: (id: string) => Promise<void>;
  onSelectProjectFilter: (projectId: string) => void;
  onOpenCloudSync: () => void;
  onUpdateProject: (id: string, requestBody: UpdateProjectRequest) => Promise<void>;
  onUpdateWorkspace: (requestBody: UpdateWorkspaceRequest) => Promise<void>;
  onUpdateWorkspaceMemberRole: (
    userId: string,
    requestBody: UpdateWorkspaceMemberRequest,
  ) => Promise<WorkspaceMemberResponse>;
  projects: ProjectResponse[];
  selectedProjectIds: string[];
  sort: SavedViewSort;
  syncRoot: SyncRootResponse | null;
  t: Translate;
  workspace: WorkspaceResponse | null;
}

export function WorkspaceHeader({
  canManageWorkspaceMetadata,
  canManageSharing,
  canSyncWorkspace,
  colorOptions,
  invitations,
  members,
  onCreateProject,
  onChangeSort,
  onCreateWorkspaceInvitation,
  onDeleteProject,
  onRemoveWorkspaceMember,
  onOpenCloudSync,
  onRevokeWorkspaceInvitation,
  onSelectProjectFilter,
  onUpdateProject,
  onUpdateWorkspace,
  onUpdateWorkspaceMemberRole,
  projects,
  selectedProjectIds,
  sort,
  syncRoot,
  t,
  workspace,
}: WorkspaceHeaderProps) {
  const [workspaceNameIsEditing, setWorkspaceNameIsEditing] = useState(false);
  const [workspaceName, setWorkspaceName] = useState(workspace?.name ?? '');
  const [workspaceColor, setWorkspaceColor] = useState(workspace?.color ?? '');
  const [editingProjectId, setEditingProjectId] = useState<string | null>(null);
  const [projectName, setProjectName] = useState('');
  const [projectColor, setProjectColor] = useState('');
  const [newProjectIsOpen, setNewProjectIsOpen] = useState(false);
  const [newProjectName, setNewProjectName] = useState('');
  const [newProjectColor, setNewProjectColor] = useState('');
  const [projectIsSubmitting, setProjectIsSubmitting] = useState(false);
  const [inviteIsOpen, setInviteIsOpen] = useState(false);
  const [focusedMemberId, setFocusedMemberId] = useState<string | null>(null);
  const [pendingRemoveMemberId, setPendingRemoveMemberId] = useState<string | null>(null);
  const [pendingDeleteProject, setPendingDeleteProject] = useState<ProjectResponse | null>(null);
  const pendingInvitations = invitations.filter(
    (invitation) => !invitation.acceptedAt && !invitation.revokedAt,
  );

  useEffect(() => {
    setWorkspaceName(workspace?.name ?? '');
    setWorkspaceColor(workspace?.color ?? '');
    setWorkspaceNameIsEditing(false);
  }, [workspace]);

  useEffect(() => {
    if (!canManageWorkspaceMetadata) {
      setWorkspaceNameIsEditing(false);
      setEditingProjectId(null);
      setNewProjectIsOpen(false);
      setPendingDeleteProject(null);
    }
  }, [canManageWorkspaceMetadata]);

  const startProjectEditing = (project: ProjectResponse) => {
    setEditingProjectId(project.id);
    setProjectName(project.name);
    setProjectColor(project.color ?? '');
    setPendingDeleteProject(null);
  };

  const cancelProjectEditing = () => {
    setEditingProjectId(null);
    setProjectName('');
    setProjectColor('');
    setPendingDeleteProject(null);
  };

  const cancelWorkspaceEditing = () => {
    setWorkspaceNameIsEditing(false);
    setWorkspaceName(workspace?.name ?? '');
  };

  const saveWorkspaceName = async () => {
    const trimmedName = workspaceName.trim();
    if (!trimmedName) {
      cancelWorkspaceEditing();
      return;
    }

    if (trimmedName === workspace?.name) {
      setWorkspaceNameIsEditing(false);
      return;
    }

    await onUpdateWorkspace({
      name: trimmedName,
      color: workspace?.color ?? null,
    });
    setWorkspaceNameIsEditing(false);
  };

  const saveProject = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = projectName.trim();
    if (!editingProjectId || !trimmedName) {
      return;
    }

    await onUpdateProject(editingProjectId, {
      name: trimmedName,
      color: projectColor.trim() || null,
    });
    cancelProjectEditing();
  };

  const createProjectFromInlineForm = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = newProjectName.trim();
    if (!trimmedName) {
      return;
    }

    setProjectIsSubmitting(true);
    try {
      await onCreateProject(trimmedName, newProjectColor);
      setNewProjectName('');
      setNewProjectColor('');
      setNewProjectIsOpen(false);
    } finally {
      setProjectIsSubmitting(false);
    }
  };

  return (
    <>
      <div
        className="workspace-header"
        style={getWorkspaceHeaderStyle(workspace?.color ?? null)}
      >
        <div className="workspace-title-block">
          <div className="workspace-title-row">
            {workspaceNameIsEditing ? (
              <form
                className="inline-heading-editor inline-heading-editor-name"
                onSubmit={(event) => {
                  event.preventDefault();
                  void saveWorkspaceName();
                }}
              >
                <input
                  aria-label={t('editBoard')}
                  autoFocus
                  className="inline-heading-input"
                  onBlur={() => void saveWorkspaceName()}
                  onChange={(event) => setWorkspaceName(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Escape') {
                      cancelWorkspaceEditing();
                    }
                  }}
                  required
                  type="text"
                  value={workspaceName}
                />
              </form>
            ) : (
              <>
                {canManageWorkspaceMetadata ? (
                  <button
                    className="heading-edit-trigger"
                    onClick={() => setWorkspaceNameIsEditing(true)}
                    title={t('editBoard')}
                    type="button"
                  >
                    <h1 id="task-board-title">
                      {workspace ? formatWorkspaceName(workspace.name, t) : 'DumpTether'}
                    </h1>
                  </button>
                ) : (
                  <h1 id="task-board-title">
                    {workspace ? formatWorkspaceName(workspace.name, t) : 'DumpTether'}
                  </h1>
                )}
                {canManageWorkspaceMetadata && workspace ? (
                  <ColorPickerPopover
                    color={workspaceColor}
                    colorOptions={colorOptions}
                    label={t('boardColor')}
                    onChange={async (color) => {
                      setWorkspaceColor(color);
                      await onUpdateWorkspace({
                        name: workspace.name,
                        color: color || null,
                      });
                    }}
                    t={t}
                  />
                ) : null}
              </>
            )}
          </div>
          <div className="member-chip-strip" aria-label={t('members')}>
            {members.slice(0, 3).map((member) => (
              <WorkspaceMemberChip
                isConfirming={pendingRemoveMemberId === member.userId}
                key={member.userId}
                member={member}
                onCancelRemove={() => setPendingRemoveMemberId(null)}
                onConfirmRemove={async () => {
                  await onRemoveWorkspaceMember(member.userId);
                  setPendingRemoveMemberId(null);
                }}
                onOpenManage={() => {
                  if (canManageSharing && !isOwnerRole(member.role)) {
                    setFocusedMemberId(member.userId);
                    setInviteIsOpen(true);
                  }
                }}
                onRequestRemove={() => setPendingRemoveMemberId(member.userId)}
                t={t}
              />
            ))}
            {members.length > 3 ? (
              <span className="member-chip">+{members.length - 3}</span>
            ) : null}
            {canManageSharing && pendingInvitations.length > 0
              ? pendingInvitations.slice(0, 2).map((invitation) => (
                  <PendingInvitationChip
                    invitation={invitation}
                    key={invitation.id}
                    onRevoke={() => onRevokeWorkspaceInvitation(invitation.id)}
                    t={t}
                  />
                ))
              : null}
            {canManageSharing && pendingInvitations.length > 2 ? (
              <span className="member-chip member-chip-muted">
                +{pendingInvitations.length - 2} {t('pendingInvites')}
              </span>
            ) : null}
            {canManageSharing ? (
              <button
                className="tiny-icon-button"
                onClick={() => {
                  setInviteIsOpen((isOpen) => !isOpen);
                }}
                title={t('inviteMember')}
                type="button"
              >
                <Icon name="plus" />
              </button>
            ) : null}
          </div>
          {inviteIsOpen ? (
            <ShareDialog
              existingTaskShares={[]}
              focusedWorkspaceMemberId={focusedMemberId}
              onClose={() => setInviteIsOpen(false)}
              onCreate={async (email, role) => {
                const created = await onCreateWorkspaceInvitation({
                  email,
                  role: role as WorkspaceMembershipRole,
                });

                return {
                  expiresAt: created.expiresAt,
                  token: created.token ?? '',
                };
              }}
              onRemoveWorkspaceMember={onRemoveWorkspaceMember}
              onRevokeTaskShare={undefined}
              onRevokeWorkspaceInvitation={onRevokeWorkspaceInvitation}
              onUpdateWorkspaceMemberRole={onUpdateWorkspaceMemberRole}
              pendingInvitations={pendingInvitations}
              roleMode="workspace"
              t={t}
              title={workspace ? formatWorkspaceName(workspace.name, t) : t('workspaces')}
              workspaceMembers={members}
            />
          ) : null}
          <div className="project-tag-strip" aria-label={t('projectTags')}>
            <button
              className="project-tag"
              data-selected={selectedProjectIds.length === 0}
              onClick={() => onSelectProjectFilter('')}
              type="button"
            >
              {t('allProjects')}
            </button>
            {projects.map((project) =>
              editingProjectId === project.id ? (
                <form
                  className="project-tag-editor"
                  key={project.id}
                  onSubmit={(event) => void saveProject(event)}
                >
                  <input
                    aria-label={t('editProject')}
                    onChange={(event) => setProjectName(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === 'Escape') {
                        cancelProjectEditing();
                      }
                    }}
                    required
                    type="text"
                    value={projectName}
                  />
                  <ColorPickerPopover
                    color={projectColor}
                    colorOptions={colorOptions}
                    label={`${project.name} ${t('color')}`}
                    onChange={setProjectColor}
                    t={t}
                  />
                  <button className="tiny-icon-button" title={t('saved')} type="submit">
                    <Icon name="check" />
                  </button>
                  <button
                    className="tiny-icon-button"
                    onClick={cancelProjectEditing}
                    title={t('cancel')}
                    type="button"
                  >
                    <Icon name="close" />
                  </button>
                  <button
                    className="tiny-icon-button danger-icon-button"
                    onClick={() => setPendingDeleteProject(project)}
                    title={t('deleteProjectTag')}
                    type="button"
                  >
                    <Icon name="trash" />
                  </button>
                </form>
              ) : (
                <span className="project-tag-wrap" key={project.id}>
                  <button
                    className="project-tag"
                    data-selected={selectedProjectIds.includes(project.id)}
                    onClick={() => onSelectProjectFilter(project.id)}
                    style={getContextChipStyle(project.color)}
                    title={project.name}
                    type="button"
                  >
                    {project.name}
                  </button>
                  {canManageWorkspaceMetadata ? (
                    <button
                      className="tiny-icon-button project-tag-edit"
                      onClick={() => startProjectEditing(project)}
                      title={t('editProject')}
                      type="button"
                    >
                      <Icon name="edit" />
                    </button>
                  ) : null}
                </span>
              ),
            )}
            {canManageWorkspaceMetadata && newProjectIsOpen ? (
              <form
                className="project-tag-editor"
                onSubmit={(event) => void createProjectFromInlineForm(event)}
              >
                <input
                  aria-label={t('newProjectTag')}
                  autoFocus
                  onChange={(event) => setNewProjectName(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Escape') {
                      setNewProjectName('');
                      setNewProjectColor('');
                      setNewProjectIsOpen(false);
                    }
                  }}
                  placeholder={t('newProjectTag')}
                  type="text"
                  value={newProjectName}
                />
                <ColorPickerPopover
                  color={newProjectColor}
                  colorOptions={colorOptions}
                  label={t('color')}
                  onChange={setNewProjectColor}
                  t={t}
                />
                <button
                  className="tiny-icon-button"
                  disabled={!newProjectName.trim() || projectIsSubmitting}
                  title={t('saved')}
                  type="submit"
                >
                  <Icon name="check" />
                </button>
                <button
                  className="tiny-icon-button"
                  onClick={() => {
                    setNewProjectName('');
                    setNewProjectColor('');
                    setNewProjectIsOpen(false);
                  }}
                  title={t('cancel')}
                  type="button"
                >
                  <Icon name="close" />
                </button>
              </form>
            ) : canManageWorkspaceMetadata ? (
              <button
                className="project-tag project-tag-add"
                onClick={() => setNewProjectIsOpen(true)}
                title={t('newProjectTag')}
                type="button"
              >
                <Icon name="plus" />
                <span>{t('newProjectTag')}</span>
              </button>
            ) : null}
          </div>
          <p>{t('wallHelp')}</p>
        </div>
        <div className="board-actions">
          <SortMenu onChange={onChangeSort} sort={sort} t={t} />
          {canSyncWorkspace ? (
            <div className="workspace-sync-actions">
              <BoardSyncStatus syncRoot={syncRoot} t={t} />
              <button
                className="sync-board-button"
                onClick={onOpenCloudSync}
                title={t('syncBoard')}
                type="button"
              >
                <Icon name="cloud" />
                <span>{t('syncBoard')}</span>
              </button>
            </div>
          ) : null}
        </div>
      </div>
      {pendingDeleteProject ? (
        <DeleteProjectDialog
          onClose={() => setPendingDeleteProject(null)}
          onDelete={async () => {
            await onDeleteProject(pendingDeleteProject.id);
            if (selectedProjectIds.includes(pendingDeleteProject.id)) {
              onSelectProjectFilter('');
            }
            cancelProjectEditing();
            setPendingDeleteProject(null);
          }}
          project={pendingDeleteProject}
          t={t}
        />
      ) : null}
    </>
  );
}

function DeleteProjectDialog({
  onClose,
  onDelete,
  project,
  t,
}: {
  onClose: () => void;
  onDelete: () => Promise<void>;
  project: ProjectResponse;
  t: Translate;
}) {
  const [isDeleting, setIsDeleting] = useState(false);

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="delete-project-title"
        aria-modal="true"
        className="delete-workspace-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('deleteProjectTag')}</p>
            <h2 id="delete-project-title">{project.name}</h2>
          </div>
          <button className="icon-button" disabled={isDeleting} onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>
        <p>{t('deleteCategoryWarning')}</p>
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
            {t('deleteCategoryNow')}
          </button>
        </div>
      </section>
    </ModalFrame>
  );
}
