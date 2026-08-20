import { Icon } from './Icon';
import { ColorOptionPicker } from './ColorOptionPicker';
import type { Translate } from '../localization';
import {
  followUpFilters,
  formatFollowUpFilter,
  type TaskWallFilters,
} from '../taskUtils';
import type { SavedViewFollowUpFilter } from '../types';

interface TaskFilterBarProps {
  filters: TaskWallFilters;
  filtersAreActive: boolean;
  onChange: (filters: TaskWallFilters) => void;
  onReset: () => void;
  options: {
    statuses: string[];
    categories: string[];
    colors: string[];
    sharedWith: string[];
  };
  t: Translate;
}

export function TaskFilterBar({
  filters,
  filtersAreActive,
  onChange,
  onReset,
  options,
  t,
}: TaskFilterBarProps) {
  const updateFilter = (update: Partial<TaskWallFilters>) => {
    onChange({ ...filters, ...update });
  };

  return (
    <div className="filter-bar" aria-label={t('filterWall')}>
      <label className="filter-search">
        <span className="sr-only">{t('filterWall')}</span>
        <input
          onChange={(event) => updateFilter({ text: event.target.value })}
          placeholder={t('filterWall')}
          type="search"
          value={filters.text}
        />
      </label>

      <select
        aria-label={t('anyStatus')}
        onChange={(event) => updateFilter({ status: event.target.value })}
        value={filters.status}
      >
        <option value="">{t('anyStatus')}</option>
        {options.statuses.map((status) => (
          <option key={status} value={status}>
            {status}
          </option>
        ))}
      </select>

      <ColorOptionPicker
        emptyLabel={t('noTaskColors')}
        label={t('color')}
        onChange={(color) => updateFilter({ color })}
        options={options.colors}
        value={filters.color}
        zeroLabel={t('anyColor')}
      />

      <select
        aria-label={t('anyFollowUp')}
        onChange={(event) =>
          updateFilter({ followUp: event.target.value as '' | SavedViewFollowUpFilter })
        }
        value={filters.followUp}
      >
        <option value="">{t('anyFollowUp')}</option>
        {followUpFilters.map((filter) => (
          <option key={filter} value={filter}>
            {formatFollowUpFilter(filter)}
          </option>
        ))}
      </select>

      <input
        aria-label={t('notTouchedDays')}
        min={1}
        onChange={(event) => updateFilter({ notTouchedDays: event.target.value })}
        placeholder={t('notTouchedDays')}
        type="number"
        value={filters.notTouchedDays}
      />

      {options.sharedWith.length > 0 ? (
        <select
          aria-label={t('sharedWith')}
          onChange={(event) => updateFilter({ sharedWith: event.target.value })}
          value={filters.sharedWith}
        >
          <option value="">{t('anySharedPerson')}</option>
          {options.sharedWith.map((email) => (
            <option key={email.toLowerCase()} value={email}>
              {email}
            </option>
          ))}
        </select>
      ) : null}

      <button
        aria-hidden={!filtersAreActive}
        className="icon-button reset-filters-button"
        disabled={!filtersAreActive}
        onClick={onReset}
        tabIndex={filtersAreActive ? 0 : -1}
        title={filtersAreActive ? t('removeFilters') : undefined}
        type="button"
      >
        <Icon name="filterOff" />
        <span className="sr-only">{t('removeFilters')}</span>
      </button>
    </div>
  );
}
