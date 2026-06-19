import { type CSSProperties } from 'react';
import { Icon, type IconName } from './Icon';
import { type Translate } from '../localization';
import { getTaskBadges } from '../taskUtils';
import type { TaskItemSummaryResponse } from '../types';

export function TaskMetaChip({
  icon,
  label,
  style,
  value,
}: {
  icon: IconName;
  label: string;
  style?: CSSProperties;
  value: string;
}) {
  return (
    <span className="task-meta-chip" style={style} title={`${label}: ${value}`}>
      <Icon name={icon} />
      {label}: {value}
    </span>
  );
}

export function TaskBadges({
  taskItem,
  t,
}: {
  taskItem: TaskItemSummaryResponse;
  t: Translate;
}) {
  const badges = getTaskBadges(taskItem, t);

  if (badges.length === 0) {
    return null;
  }

  return (
    <span className="task-badges" aria-label={badges.join(', ')}>
      {badges.map((badge) => (
        <span className="task-badge" key={badge}>
          {badge}
        </span>
      ))}
    </span>
  );
}
