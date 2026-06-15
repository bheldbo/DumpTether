import type { CSSProperties } from 'react';
import type { Translate } from './localization';
import type {
  ProjectResponse,
  SavedViewFollowUpFilter,
  TaskItemSummaryResponse,
} from './types';

export interface TaskWallFilters {
  text: string;
  status: string;
  category: string;
  color: string;
  projectId: string;
  notTouchedDays: string;
  followUp: '' | SavedViewFollowUpFilter;
  sharedWith: string;
  sharedWithMe: boolean;
}

export const followUpFilters: SavedViewFollowUpFilter[] = [
  'Any',
  'Overdue',
  'Today',
  'ThisWeek',
];

export const emptyTaskWallFilters: TaskWallFilters = {
  text: '',
  status: '',
  category: '',
  color: '',
  projectId: '',
  notTouchedDays: '',
  followUp: '',
  sharedWith: '',
  sharedWithMe: false,
};

export const colorChoices = [
  '#FDE68A',
  '#FCA5A5',
  '#93C5FD',
  '#86EFAC',
  '#C4B5FD',
  '#FDBA74',
  '#CBD5E1',
];

export function buildTaskFilterOptions(
  taskItems: TaskItemSummaryResponse[],
  colorOptions: string[],
) {
  return {
    statuses: uniqueSorted(taskItems.map((taskItem) => taskItem.status)),
    categories: uniqueSorted(taskItems.flatMap((taskItem) => splitTaskCategories(taskItem.category))),
    colors: colorOptions,
  };
}

export function getTaskColors(taskItems: TaskItemSummaryResponse[]) {
  return taskItems
    .map((taskItem) => taskItem.color)
    .filter((color): color is string => Boolean(color));
}

export function mergeColorOptions(...sources: string[][]) {
  return Array.from(
    new Set(
      sources
        .flat()
        .map((color) => color.trim().toUpperCase())
        .filter(isHexColor),
    ),
  );
}

export function splitTaskCategories(category: string | null | undefined) {
  if (!category) {
    return [];
  }

  return Array.from(
    new Set(
      category
        .split(';')
        .map((value) => value.trim())
        .filter(Boolean),
    ),
  );
}

export function joinTaskCategories(categories: string[]) {
  const normalized = Array.from(
    new Set(
      categories
        .map((category) => category.trim())
        .filter(Boolean),
    ),
  );

  return normalized.length > 0 ? normalized.join('; ') : null;
}

export function getProjectsForTaskCategories(
  category: string | null | undefined,
  projects: ProjectResponse[] | Map<string, ProjectResponse>,
) {
  const projectLookup = projects instanceof Map
    ? projects
    : new Map(projects.map((project) => [project.name.toLowerCase(), project]));

  return splitTaskCategories(category)
    .map((categoryName) => projectLookup.get(categoryName.toLowerCase()) ?? null)
    .filter((project): project is ProjectResponse => Boolean(project));
}

export function getPrimaryProjectIdForCategories(
  category: string | null | undefined,
  projects: ProjectResponse[],
) {
  const firstProject = getProjectsForTaskCategories(category, projects)[0];
  return firstProject?.id ?? null;
}

export function applyTaskWallFilters(
  taskItems: TaskItemSummaryResponse[],
  filters: TaskWallFilters,
  currentUserEmail: string | null,
  projects: ProjectResponse[],
) {
  const text = filters.text.trim().toLowerCase();
  const sharedWith = filters.sharedWith.trim().toLowerCase();
  const normalizedCurrentUserEmail = currentUserEmail?.trim().toLowerCase() ?? '';
  const notTouchedDays = numberOrNull(filters.notTouchedDays);

  return taskItems.filter((taskItem) => {
    if (text && !taskMatchesText(taskItem, text)) {
      return false;
    }

    if (filters.status && taskItem.status !== filters.status) {
      return false;
    }

    if (filters.category && !taskHasCategory(taskItem, filters.category)) {
      return false;
    }

    if (filters.color && taskItem.color !== filters.color) {
      return false;
    }

    if (filters.projectId && !taskMatchesProjectFilter(taskItem, filters.projectId, projects)) {
      return false;
    }

    if (filters.followUp && !taskMatchesFollowUp(taskItem, filters.followUp)) {
      return false;
    }

    if (notTouchedDays && !isNotTouchedForDays(taskItem, notTouchedDays)) {
      return false;
    }

    if (sharedWith &&
        !taskItem.shares.some((share) => share.email.toLowerCase().includes(sharedWith))) {
      return false;
    }

    if (filters.sharedWithMe &&
        !taskItem.shares.some((share) =>
          share.email.toLowerCase() === normalizedCurrentUserEmail)) {
      return false;
    }

    return true;
  });
}

export function taskWallFiltersAreActive(filters: TaskWallFilters) {
  return Object.values(filters).some((value) =>
    typeof value === 'boolean' ? value : value.trim().length > 0,
  );
}

export function getTaskCardStyle(color: string | null) {
  const taskColor = color && isHexColor(color) ? color : '#FFF3A6';
  const textColor = getReadableTextColor(taskColor);

  return {
    '--task-note-color': taskColor,
    '--task-note-text': textColor,
    '--task-note-chip-bg':
      textColor === '#FFFFFF'
        ? 'rgba(255, 255, 255, 0.16)'
        : 'rgba(255, 255, 255, 0.46)',
    '--task-note-chip-border':
      textColor === '#FFFFFF'
        ? 'rgba(255, 255, 255, 0.24)'
        : 'rgba(24, 33, 44, 0.1)',
  } as CSSProperties;
}

export function getWorkspaceHeaderStyle(
  workspaceColor: string | null,
  projectColor: string | null,
) {
  const baseColor = workspaceColor && isHexColor(workspaceColor)
    ? workspaceColor
    : '#E8F3F0';
  const accentColor = projectColor && isHexColor(projectColor)
    ? projectColor
    : baseColor;

  return {
    '--workspace-color': baseColor,
    '--project-color': accentColor,
    '--workspace-text': getReadableTextColor(baseColor),
  } as CSSProperties;
}

export function getSidebarStyle(workspaceColor: string | null) {
  const baseColor = workspaceColor && isHexColor(workspaceColor)
    ? workspaceColor
    : '#184C48';

  return {
    '--sidebar-workspace-color': baseColor,
    '--sidebar-workspace-text': getReadableTextColor(baseColor),
  } as CSSProperties;
}

export function getContextChipStyle(color: string | null) {
  if (!color || !isHexColor(color)) {
    return undefined;
  }

  return {
    '--context-chip-color': color,
    '--context-chip-text': getReadableTextColor(color),
  } as CSSProperties;
}

export function getTaskState(taskItem: TaskItemSummaryResponse) {
  if (taskItem.archivedAt) {
    return 'archived';
  }

  if (isFollowUpOverdue(taskItem)) {
    return 'overdue';
  }

  if (isWaiting(taskItem)) {
    return 'waiting';
  }

  if (isStale(taskItem)) {
    return 'stale';
  }

  return 'active';
}

export function getTaskBadges(taskItem: TaskItemSummaryResponse, t: Translate) {
  const badges: string[] = [];

  if (taskItem.archivedAt) {
    badges.push(t('archive'));
  }

  if (isWaiting(taskItem)) {
    badges.push(t('waiting'));
  }

  if (isStale(taskItem)) {
    badges.push(t('stale'));
  }

  return badges;
}

export function isHexColor(value: string) {
  return /^#[0-9A-F]{6}$/i.test(value);
}

export function getFollowUpTone(value: string | null) {
  if (!value) {
    return 'none';
  }

  const followUpDate = new Date(value);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  const followUpDay = new Date(
    followUpDate.getFullYear(),
    followUpDate.getMonth(),
    followUpDate.getDate(),
  );

  if (followUpDay.getTime() < today.getTime()) {
    return 'overdue';
  }

  if (followUpDay.getTime() < tomorrow.getTime()) {
    return 'today';
  }

  return 'future';
}

export function formatFollowUpFilter(value: SavedViewFollowUpFilter) {
  return value
    .replace('ThisWeek', 'This week')
    .replace('Any', 'Has follow-up');
}

export function uniqueSorted(values: Array<string | null>) {
  return Array.from(
    new Set(values.filter((value): value is string => Boolean(value))),
  ).sort((left, right) => left.localeCompare(right));
}

function taskMatchesText(taskItem: TaskItemSummaryResponse, text: string) {
  return [
    taskItem.title,
    taskItem.status,
    taskItem.category,
    taskItem.latestTimelineEntry?.details,
    ...taskItem.shares.map((share) => share.email),
  ].some((value) => value?.toLowerCase().includes(text));
}

function taskMatchesProjectFilter(
  taskItem: TaskItemSummaryResponse,
  projectId: string,
  projects: ProjectResponse[],
) {
  if (taskItem.projectId === projectId) {
    return true;
  }

  const project = projects.find((candidate) => candidate.id === projectId);
  return project ? taskHasCategory(taskItem, project.name) : false;
}

function taskHasCategory(taskItem: TaskItemSummaryResponse, category: string) {
  return splitTaskCategories(taskItem.category).some((taskCategory) =>
    taskCategory.toLowerCase() === category.trim().toLowerCase());
}

function taskMatchesFollowUp(
  taskItem: TaskItemSummaryResponse,
  followUp: SavedViewFollowUpFilter,
) {
  if (!taskItem.followUpAt) {
    return false;
  }

  const followUpAt = new Date(taskItem.followUpAt);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  const nextWeek = new Date(today);
  nextWeek.setDate(today.getDate() + 7);

  switch (followUp) {
    case 'Overdue':
      return getFollowUpTone(taskItem.followUpAt) === 'overdue';
    case 'Today':
      return followUpAt >= today && followUpAt < tomorrow;
    case 'ThisWeek':
      return followUpAt >= today && followUpAt < nextWeek;
    case 'Any':
    default:
      return true;
  }
}

function isNotTouchedForDays(taskItem: TaskItemSummaryResponse, days: number) {
  const threshold = Date.now() - days * 24 * 60 * 60 * 1000;
  return new Date(taskItem.lastTouchedAt).getTime() <= threshold;
}

function getReadableTextColor(hexColor: string) {
  const red = Number.parseInt(hexColor.slice(1, 3), 16);
  const green = Number.parseInt(hexColor.slice(3, 5), 16);
  const blue = Number.parseInt(hexColor.slice(5, 7), 16);
  const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255;

  return luminance > 0.55 ? '#18212C' : '#FFFFFF';
}

function isWaiting(taskItem: TaskItemSummaryResponse) {
  return taskItem.status?.toLowerCase().includes('waiting') ?? false;
}

function isFollowUpOverdue(taskItem: TaskItemSummaryResponse) {
  return Boolean(taskItem.followUpAt) &&
    !taskItem.archivedAt &&
    getFollowUpTone(taskItem.followUpAt) === 'overdue';
}

function isStale(taskItem: TaskItemSummaryResponse) {
  return !taskItem.archivedAt && isNotTouchedForDays(taskItem, 14);
}

function numberOrNull(value: string) {
  if (!value.trim()) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
