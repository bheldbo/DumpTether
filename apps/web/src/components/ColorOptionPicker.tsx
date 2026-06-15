import { useEffect, useRef, useState } from 'react';

interface ColorOptionPickerProps {
  emptyLabel: string;
  label: string;
  onChange: (color: string) => void;
  options: string[];
  value: string;
  zeroLabel: string;
}

export function ColorOptionPicker({
  emptyLabel,
  label,
  onChange,
  options,
  value,
  zeroLabel,
}: ColorOptionPickerProps) {
  const [isOpen, setIsOpen] = useState(false);
  const pickerRef = useRef<HTMLDivElement>(null);
  const selectedColor = options.find((color) => color === value) ?? '';

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        pickerRef.current &&
        event.target instanceof Node &&
        !pickerRef.current.contains(event.target)
      ) {
        setIsOpen(false);
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);

    return () => window.removeEventListener('pointerdown', handlePointerDown);
  }, [isOpen]);

  const chooseColor = (color: string) => {
    onChange(color);
    setIsOpen(false);
  };

  return (
    <div className="color-option-picker" ref={pickerRef}>
      <button
        aria-expanded={isOpen}
        aria-label={label}
        className="color-option-trigger"
        onClick={() => setIsOpen((open) => !open)}
        type="button"
      >
        {selectedColor ? (
          <>
            <span className="color-option-swatch" style={{ backgroundColor: selectedColor }} />
            <span className="color-option-code">{selectedColor}</span>
          </>
        ) : (
          <>
            <span className="color-option-empty" />
            <span>{zeroLabel}</span>
          </>
        )}
      </button>

      {isOpen ? (
        <div className="color-option-menu" role="listbox">
          <button
            className="color-option-button"
            data-selected={!value}
            onClick={() => chooseColor('')}
            type="button"
          >
            <span className="color-option-empty" />
            <span>{zeroLabel}</span>
          </button>
          {options.map((color) => (
            <button
              className="color-option-button"
              data-selected={value.toUpperCase() === color}
              key={color}
              onClick={() => chooseColor(color)}
              title={color}
              type="button"
            >
              <span className="color-option-swatch" style={{ backgroundColor: color }} />
              <span className="color-option-code">{color}</span>
            </button>
          ))}
          {options.length === 0 ? (
            <span className="color-option-empty-text">{emptyLabel}</span>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
