import {
  type CSSProperties,
  type FormEvent,
  type KeyboardEvent,
  useRef,
  useState,
} from 'react';
import { Icon } from '../../../components/Icon';
import type {
  CreateTaskItemRequest,
  TaskItemSummaryResponse,
} from '../../../types';
import './SubtaskWall.css';

export interface SubtaskWallStrings {
  heading: string;
  createPlaceholder: string;
  createButtonLabel: string;
  creatingLabel: string;
  loadingMessage: string;
  emptyMessage: string;
  errorMessage: string;
  createErrorMessage: string;
  retryLabel: string;
  formatNoteCount: (count: number) => string;
  formatSubtaskCount: (count: number) => string;
  formatUpdatedAt: (value: string) => string;
}

export interface SubtaskWallProps {
  parentTaskItemId: string;
  subtasks: readonly TaskItemSummaryResponse[];
  strings: SubtaskWallStrings;
  canCreate?: boolean;
  isLoading?: boolean;
  error?: string | null;
  createRequestDefaults?: Omit<
    CreateTaskItemRequest,
    'title' | 'parentTaskItemId'
  >;
  onCreate: (request: CreateTaskItemRequest) => Promise<void>;
  onOpenSubtask: (subtask: TaskItemSummaryResponse) => void;
  onRetry?: () => void;
}

export function SubtaskWall({
  parentTaskItemId,
  subtasks,
  strings,
  canCreate = true,
  isLoading = false,
  error = null,
  createRequestDefaults,
  onCreate,
  onOpenSubtask,
  onRetry,
}: SubtaskWallProps) {
  const [title, setTitle] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const hasLoadError = error !== null;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const nextTitle = title.trim();
    if (!nextTitle || isCreating) {
      return;
    }

    setIsCreating(true);
    setCreateError(null);

    try {
      const request = {
        ...createRequestDefaults,
        title: nextTitle,
        parentTaskItemId,
      } as CreateTaskItemRequest;

      await onCreate(request);
      setTitle('');
      requestAnimationFrame(() => inputRef.current?.focus());
    } catch {
      setCreateError(strings.createErrorMessage);
      requestAnimationFrame(() => inputRef.current?.focus());
    } finally {
      setIsCreating(false);
    }
  }

  function handleInputKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key !== 'Enter' || event.nativeEvent.isComposing) {
      return;
    }

    event.preventDefault();
    event.currentTarget.form?.requestSubmit();
  }

  return (
    <section className="subtask-wall" aria-labelledby={`subtasks-${parentTaskItemId}`}>
      <header className="subtask-wall__header">
        <span className="subtask-wall__heading-icon" aria-hidden="true">
          <Icon name="subtasks" />
        </span>
        <h2 id={`subtasks-${parentTaskItemId}`}>{strings.heading}</h2>
        {isLoading && subtasks.length > 0 ? (
          <span className="subtask-wall__quiet-spinner" aria-hidden="true" />
        ) : null}
      </header>

      {canCreate ? (
        <form className="subtask-wall__create" onSubmit={handleSubmit}>
          <input
            ref={inputRef}
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            onKeyDown={handleInputKeyDown}
            placeholder={strings.createPlaceholder}
            aria-label={strings.createPlaceholder}
            disabled={isCreating}
          />
          <button
            type="submit"
            className="subtask-wall__create-button"
            disabled={!title.trim() || isCreating}
            aria-label={isCreating ? strings.creatingLabel : strings.createButtonLabel}
            title={isCreating ? strings.creatingLabel : strings.createButtonLabel}
          >
            {isCreating ? (
              <span className="subtask-wall__spinner" aria-hidden="true" />
            ) : (
              <Icon name="plus" />
            )}
          </button>
        </form>
      ) : null}

      {createError ? (
        <p className="subtask-wall__inline-error" role="alert">
          <Icon name="help" />
          {createError}
        </p>
      ) : null}

      {hasLoadError ? (
        <div className="subtask-wall__state subtask-wall__state--error" role="alert">
          <Icon name="help" />
          <p>{error || strings.errorMessage}</p>
          {onRetry ? (
            <button type="button" onClick={onRetry} aria-label={strings.retryLabel}>
              <Icon name="refresh" />
              <span>{strings.retryLabel}</span>
            </button>
          ) : null}
        </div>
      ) : isLoading && subtasks.length === 0 ? (
        <div className="subtask-wall__state" role="status">
          <span className="subtask-wall__spinner" aria-hidden="true" />
          <p>{strings.loadingMessage}</p>
        </div>
      ) : subtasks.length === 0 ? (
        <div className="subtask-wall__state subtask-wall__state--empty">
          <Icon name="subtasks" />
          <p>{strings.emptyMessage}</p>
        </div>
      ) : (
        <div className="subtask-wall__grid">
          {subtasks.map((subtask) => {
            const subtaskCount = subtask.subtaskCount ?? 0;
            const style = {
              '--subtask-note-color': subtask.color || undefined,
            } as CSSProperties;

            return (
              <button
                key={subtask.id}
                type="button"
                className="subtask-wall__card"
                style={style}
                onClick={() => onOpenSubtask(subtask)}
              >
                <span className="subtask-wall__card-title">{subtask.title}</span>
                {subtask.status ? (
                  <span className="subtask-wall__status">{subtask.status}</span>
                ) : null}
                <span className="subtask-wall__card-meta">
                  <span>
                    <Icon name="clock" />
                    {strings.formatUpdatedAt(subtask.lastTouchedAt)}
                  </span>
                  {subtask.noteCount > 0 ? (
                    <span>{strings.formatNoteCount(subtask.noteCount)}</span>
                  ) : null}
                  {subtaskCount > 0 ? (
                    <span>{strings.formatSubtaskCount(subtaskCount)}</span>
                  ) : null}
                </span>
              </button>
            );
          })}
        </div>
      )}
    </section>
  );
}
