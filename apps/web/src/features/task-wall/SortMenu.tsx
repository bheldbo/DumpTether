import { useEffect, useRef, useState } from 'react';
import { formatSortField } from '../../appUtils';
import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';
import type { SavedViewSort } from '../../types';

const sortFields = [
  'lastTouchedAt',
  'createdAt',
  'followUpAt',
  'title',
  'status',
] as const;

export function SortMenu({
  onChange,
  sort,
  t,
}: {
  onChange: (sort: SavedViewSort) => void;
  sort: SavedViewSort;
  t: Translate;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const field = sort.field ?? 'lastTouchedAt';
  const direction = sort.direction ?? 'desc';
  const selectedLabel = `${formatSortField(field, t)} · ${
    direction === 'asc' ? t('sortAscending') : t('sortDescending')
  }`;

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        event.target instanceof Node &&
        rootRef.current &&
        !rootRef.current.contains(event.target)
      ) {
        setIsOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    };

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  return (
    <div className="sort-menu" ref={rootRef}>
      <button
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        className="sort-menu-trigger"
        onClick={() => setIsOpen((current) => !current)}
        type="button"
      >
        <span className="sort-menu-label">{t('sortedBy')}</span>
        <span className="sort-menu-value">{selectedLabel}</span>
        <Icon name="chevronDown" />
      </button>
      {isOpen ? (
        <div
          aria-label={t('sortedBy')}
          className="sort-menu-options"
          role="listbox"
        >
          {sortFields.flatMap((sortField) =>
            (['desc', 'asc'] as const).map((sortDirection) => {
              const isSelected =
                sortField === field && sortDirection === direction;
              const label = `${formatSortField(sortField, t)} · ${
                sortDirection === 'asc'
                  ? t('sortAscending')
                  : t('sortDescending')
              }`;

              return (
                <button
                  aria-selected={isSelected}
                  className="sort-menu-option"
                  key={`${sortField}:${sortDirection}`}
                  onClick={() => {
                    onChange({
                      field: sortField,
                      direction: sortDirection,
                    });
                    setIsOpen(false);
                  }}
                  role="option"
                  type="button"
                >
                  <span>{label}</span>
                  {isSelected ? <Icon name="check" /> : null}
                </button>
              );
            }),
          )}
        </div>
      ) : null}
    </div>
  );
}
