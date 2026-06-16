import { useEffect, useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { type Translate } from '../../localization';
import { getContextChipStyle } from '../../taskUtils';
import type { ProjectResponse } from '../../types';

export function CategoryMultiSelect({
  disabled,
  onCancel,
  onCommit,
  projects,
  selectedCategories,
  t,
}: {
  disabled: boolean;
  onCancel: () => void;
  onCommit: (categories: string[]) => void;
  projects: ProjectResponse[];
  selectedCategories: string[];
  t: Translate;
}) {
  const [draftCategories, setDraftCategories] = useState(selectedCategories);
  const pickerRef = useRef<HTMLDivElement>(null);
  const selectedNames = new Set(draftCategories.map((category) => category.toLowerCase()));

  useEffect(() => {
    setDraftCategories(selectedCategories);
  }, [selectedCategories]);

  useEffect(() => {
    const handlePointerDown = (event: PointerEvent) => {
      if (
        pickerRef.current &&
        event.target instanceof Node &&
        !pickerRef.current.contains(event.target)
      ) {
        onCommit(draftCategories);
      }
    };

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onCancel();
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);
    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('pointerdown', handlePointerDown);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [draftCategories, onCancel, onCommit]);

  const toggleCategory = (project: ProjectResponse) => {
    const hasCategory = selectedNames.has(project.name.toLowerCase());
    const nextCategories = hasCategory
      ? draftCategories.filter((category) =>
        category.toLowerCase() !== project.name.toLowerCase())
      : [...draftCategories, project.name];

    setDraftCategories(nextCategories);
  };

  return (
    <div
      className="category-multi-select"
      onClick={(event) => event.stopPropagation()}
      ref={pickerRef}
    >
      <div className="category-option-list">
        {projects.length === 0 ? (
          <span className="context-muted">{t('noCategoriesYet')}</span>
        ) : (
          projects.map((project) => {
            const isSelected = selectedNames.has(project.name.toLowerCase());

            return (
              <button
                className="category-option"
                data-selected={isSelected}
                disabled={disabled}
                key={project.id}
                onClick={() => toggleCategory(project)}
                style={getContextChipStyle(project.color)}
                type="button"
              >
                <span className="category-option-check">
                  {isSelected ? <Icon name="check" /> : null}
                </span>
                <span>{project.name}</span>
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}

