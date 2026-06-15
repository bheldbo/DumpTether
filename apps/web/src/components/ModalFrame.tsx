import type { MouseEvent, ReactNode } from 'react';

interface ModalFrameProps {
  children: ReactNode;
  className?: string;
  onClose: () => void;
}

export function ModalFrame({
  children,
  className = 'dialog-backdrop',
  onClose,
}: ModalFrameProps) {
  const closeFromBackdrop = (event: MouseEvent<HTMLDivElement>) => {
    if (event.target === event.currentTarget) {
      onClose();
    }
  };

  return (
    <div className={className} onClick={closeFromBackdrop} role="presentation">
      {children}
    </div>
  );
}
