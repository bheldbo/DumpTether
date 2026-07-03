import type { AuthClientOptionsResponse, FieldDefinitionScope, FieldDefinitionType } from './types';

export type WorkspaceMode = 'tasks' | 'templates';
export type SettingsSectionKey = 'general' | 'statuses' | 'archive' | 'cleanup';

export interface EditableTemplateField {
  clientId: string;
  id?: string;
  name: string;
  type: FieldDefinitionType;
  scope: FieldDefinitionScope;
  required: boolean;
  sortOrder: number;
  optionsText: string;
  layoutRow: number;
  layoutColumn: number;
  layoutRowSpan: number;
  layoutColumnSpan: number;
  layoutWeight: number;
}

export const fieldTypes: FieldDefinitionType[] = [
  'Text',
  'LongText',
  'Date',
  'Checkbox',
  'Select',
];

export const languageStorageKey = 'dumptether.language';
export const workspaceStorageKey = 'dumptether.workspace';
export const statusOptionsStorageKey = 'dumptether.statusOptions';
export const sidebarWidthStorageKey = 'dumptether.sidebarWidth';
export const syncCloudBaseUrlStorageKey = 'dumptether.syncCloudBaseUrl';

export const minSidebarWidth = 232;
export const maxSidebarWidth = 440;

export const defaultAuthOptions: AuthClientOptionsResponse = {
  requiresAuthentication: true,
  guestSessionsEnabled: true,
  developmentLoginEnabled: false,
  emailConfirmationEnabled: false,
  signupMode: 'Open',
  oAuthProviders: [],
};

export type ConnectionStatus = 'checking' | 'online' | 'offline';
export type ToastTone = 'info' | 'warning' | 'error';

export interface ToastMessage {
  id: number;
  tone: ToastTone;
  message: string;
}
