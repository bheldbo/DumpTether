import { Icon } from './Icon';

interface ToastStackProps {
  onDismiss: (id: number) => void;
  toasts: Array<{
    id: number;
    message: string;
    tone: 'info' | 'warning' | 'error';
  }>;
}

export function ToastStack({ onDismiss, toasts }: ToastStackProps) {
  if (toasts.length === 0) {
    return null;
  }

  return (
    <div className="toast-stack" role="status" aria-live="polite">
      {toasts.map((toast) => (
        <div className="toast" data-tone={toast.tone} key={toast.id}>
          <span>{toast.message}</span>
          <button
            className="toast-close"
            onClick={() => onDismiss(toast.id)}
            title="Close"
            type="button"
          >
            <Icon name="close" />
          </button>
        </div>
      ))}
    </div>
  );
}
