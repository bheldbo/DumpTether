import { type CSSProperties, useEffect, useMemo, useRef, useState } from 'react';
import { Icon } from './Icon';
import { type Translate } from '../localization';
import { colorChoices, isHexColor, mergeColorOptions } from '../taskUtils';

export function ColorPickerPopover({
  color,
  colorOptions,
  label,
  onChange,
  placement = 'below',
  t,
}: {
  color: string;
  colorOptions?: string[];
  label: string;
  onChange: (color: string) => void;
  placement?: 'below' | 'left' | 'leftWide';
  t: Translate;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [draftColor, setDraftColor] = useState('');
  const draftColorRef = useRef('');
  const onChangeRef = useRef(onChange);
  const popoverRef = useRef<HTMLDivElement>(null);
  const selectedColor = isHexColor(color) ? color : '#FDE68A';
  const choices = useMemo(
    () => mergeColorOptions(colorOptions ?? [], colorChoices, color ? [color] : []),
    [color, colorOptions],
  );

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const initialColor = isHexColor(color) ? color.toUpperCase() : selectedColor;
    draftColorRef.current = initialColor;
    setDraftColor(initialColor);

    const handlePointerDown = (event: PointerEvent) => {
      if (
        popoverRef.current &&
        event.target instanceof Node &&
        !popoverRef.current.contains(event.target)
      ) {
        const nextColor = draftColorRef.current.trim().toUpperCase();
        onChangeRef.current(isHexColor(nextColor) ? nextColor : '');
        setIsOpen(false);
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);

    return () => window.removeEventListener('pointerdown', handlePointerDown);
  }, [color, isOpen, selectedColor]);

  const updateDraftColor = (nextColor: string) => {
    draftColorRef.current = nextColor;
    setDraftColor(nextColor);
  };

  const commitColor = () => {
    const nextColor = draftColorRef.current.trim().toUpperCase();
    onChange(isHexColor(nextColor) ? nextColor : '');
    setIsOpen(false);
  };

  const cancelColor = () => {
    updateDraftColor(isHexColor(color) ? color.toUpperCase() : selectedColor);
    setIsOpen(false);
  };

  return (
    <div
      className="color-popover"
      data-placement={placement}
      onClick={(event) => event.stopPropagation()}
      onPointerDown={(event) => event.stopPropagation()}
      ref={popoverRef}
    >
      <button
        aria-expanded={isOpen}
        aria-label={label}
        className="color-trigger"
        onClick={(event) => {
          event.stopPropagation();
          setIsOpen((open) => !open);
        }}
        style={{ '--picker-color': color || '#FFFFFF' } as CSSProperties}
        title={label}
        type="button"
      >
        <span className="color-trigger-dot" />
        <Icon name="edit" />
      </button>
      {isOpen ? (
        <div className="color-popover-panel">
          <div className="color-swatch-row" aria-label={label}>
            {choices.map((choice) => (
              <button
                aria-label={`Use ${choice}`}
                className="color-swatch"
                data-selected={draftColor.toUpperCase() === choice}
                key={choice}
                onClick={(event) => {
                  event.stopPropagation();
                  updateDraftColor(choice);
                  onChange(choice);
                  setIsOpen(false);
                }}
                style={{ backgroundColor: choice }}
                type="button"
              />
            ))}
            <span className="color-popover-code">{draftColor || t('noColor')}</span>
          </div>
          <div className="custom-color-row">
            <input
              aria-label="Custom color"
              onChange={(event) => updateDraftColor(event.target.value.toUpperCase())}
              onClick={(event) => event.stopPropagation()}
              onPointerDown={(event) => event.stopPropagation()}
              type="color"
              value={isHexColor(draftColor) ? draftColor : selectedColor}
            />
            <input
              aria-label={t('taskColor')}
              className="custom-color-input"
              onChange={(event) => updateDraftColor(event.target.value.toUpperCase())}
              onClick={(event) => event.stopPropagation()}
              onPointerDown={(event) => event.stopPropagation()}
              placeholder="#FDE68A"
              type="text"
              value={draftColor}
            />
          </div>
          <div className="color-popover-actions">
            <button
              className="tiny-icon-button"
              disabled={!isHexColor(draftColor)}
              onClick={(event) => {
                event.stopPropagation();
                commitColor();
              }}
              title={t('saved')}
              type="button"
            >
              <Icon name="check" />
            </button>
            <button
              className="tiny-icon-button"
              onClick={(event) => {
                event.stopPropagation();
                cancelColor();
              }}
              title={t('cancel')}
              type="button"
            >
              <Icon name="close" />
            </button>
          </div>
          {color ? (
            <button
              className="clear-color-button"
              onClick={(event) => {
                event.stopPropagation();
                onChange('');
                setIsOpen(false);
              }}
              type="button"
            >
              {t('clearColor')}
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

