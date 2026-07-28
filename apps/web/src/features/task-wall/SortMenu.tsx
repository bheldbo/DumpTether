import {
  type CSSProperties,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from 'react';
import { createPortal } from 'react-dom';
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
  const [optionsStyle, setOptionsStyle] = useState<CSSProperties>({});
  const rootRef = useRef<HTMLDivElement>(null);
  const optionsRef = useRef<HTMLDivElement>(null);
  const field = sort.field ?? 'lastTouchedAt';
  const direction = sort.direction ?? 'desc';
  const selectedLabel = `${formatSortField(field, t)}, ${
    direction === 'asc' ? t('sortAscending') : t('sortDescending')
  }`;

  const positionOptions = useCallback(() => {
    const trigger = rootRef.current;
    if (!trigger) {
      return;
    }

    const viewportPadding = 12;
    const gap = 6;
    const triggerRect = trigger.getBoundingClientRect();
    const width = Math.min(
      Math.max(triggerRect.width, 280),
      window.innerWidth - viewportPadding * 2,
    );
    const left = Math.min(
      Math.max(viewportPadding, triggerRect.right - width),
      window.innerWidth - width - viewportPadding,
    );
    const roomBelow = window.innerHeight - triggerRect.bottom - viewportPadding;
    const roomAbove = triggerRect.top - viewportPadding;
    const openAbove = roomBelow < 220 && roomAbove > roomBelow;
    const availableHeight = Math.max(
      160,
      Math.min(360, (openAbove ? roomAbove : roomBelow) - gap),
    );

    setOptionsStyle({
      left,
      maxHeight: availableHeight,
      width,
      ...(openAbove
        ? { bottom: window.innerHeight - triggerRect.top + gap, top: 'auto' }
        : { bottom: 'auto', top: triggerRect.bottom + gap }),
    });
  }, []);

  useLayoutEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    positionOptions();
    window.addEventListener('resize', positionOptions);
    window.addEventListener('scroll', positionOptions, true);
    return () => {
      window.removeEventListener('resize', positionOptions);
      window.removeEventListener('scroll', positionOptions, true);
    };
  }, [isOpen, positionOptions]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        event.target instanceof Node &&
        rootRef.current &&
        !rootRef.current.contains(event.target) &&
        !optionsRef.current?.contains(event.target)
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

  const options = (
    <div
      aria-label={t('sortedBy')}
      className="sort-menu-options"
      ref={optionsRef}
      role="listbox"
      style={optionsStyle}
    >
      {sortFields.flatMap((sortField) =>
        (['desc', 'asc'] as const).map((sortDirection) => {
          const isSelected =
            sortField === field && sortDirection === direction;
          const label = `${formatSortField(sortField, t)}, ${
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
  );

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
      {isOpen ? createPortal(options, document.body) : null}
    </div>
  );
}
