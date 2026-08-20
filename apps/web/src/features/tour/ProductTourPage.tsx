import { useEffect, useMemo, useState } from 'react';
import { isDesktopRuntime } from '../../clientRuntime';
import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';
import { GuidedTourOverlay } from './GuidedTourOverlay';
import { TourTemplateStudio } from './TourTemplateStudio';
import {
  buildTourBoards,
  buildTourGuideSteps,
  type TourBoardId,
  type TourColor,
  type TourFollowUp,
  type TourMember,
  type TourSurface,
  type TourTask,
} from './tourData';

const guideDismissedKey = 'dumptether:tour-guide-dismissed:v1';

export function ProductTourPage({ onClose, t }: { onClose: () => void; t: Translate }) {
  const boards = useMemo(() => buildTourBoards(t), [t]);
  const desktopRuntime = isDesktopRuntime();
  const guideSteps = useMemo(
    () => buildTourGuideSteps(t, desktopRuntime),
    [desktopRuntime, t],
  );
  const [surface, setSurface] = useState<TourSurface>('wall');
  const [boardId, setBoardId] = useState<TourBoardId>('work');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [color, setColor] = useState<'all' | TourColor>('all');
  const [followUp, setFollowUp] = useState<'all' | Exclude<TourFollowUp, 'none'>>('all');
  const [category, setCategory] = useState('all');
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [guideIndex, setGuideIndex] = useState<number | null>(() => {
    try {
      return window.localStorage.getItem(guideDismissedKey) === 'true' ? null : 0;
    } catch {
      return 0;
    }
  });
  const board = boards.find((candidate) => candidate.id === boardId) ?? boards[0];
  const selectedTask = board.tasks.find((task) => task.id === selectedTaskId) ?? null;
  const normalizedSearch = search.trim().toLocaleLowerCase();
  const visibleTasks = board.tasks.filter((task) =>
    (normalizedSearch.length === 0 || `${task.title} ${task.note} ${task.description} ${task.category} ${task.status}`.toLocaleLowerCase().includes(normalizedSearch)) &&
    (status === 'all' || task.status === status) &&
    (color === 'all' || task.color === color) &&
    (followUp === 'all' || task.followUp === followUp) &&
    (category === 'all' || task.category === category));

  useEffect(() => {
    if (guideIndex === null) {
      return;
    }

    const step = guideSteps[guideIndex];
    setSurface(step.surface);
    if (step.boardId) {
      setBoardId(step.boardId);
      setCategory('all');
      setSearch('');
    }
    setSelectedTaskId(step.taskId ?? null);
  }, [guideIndex, guideSteps]);

  const dismissGuide = () => {
    setGuideIndex(null);
    try {
      window.localStorage.setItem(guideDismissedKey, 'true');
    } catch {
      // The tour still works when storage is unavailable.
    }
  };

  const selectBoard = (nextBoardId: TourBoardId) => {
    setBoardId(nextBoardId);
    setSearch('');
    setStatus('all');
    setColor('all');
    setFollowUp('all');
    setCategory('all');
    setSelectedTaskId(null);
  };

  return (
    <section className="product-tour-page">
      <header className="tour-intro-band">
        <div>
          <p className="detail-kicker">{t('tourKicker')}</p>
          <h1>{t('tourTitle')}</h1>
          <p>{t('tourIntro')}</p>
        </div>
        <div className="tour-intro-actions">
          <span><Icon name="shield" /> {t('tourExamplesOnly')}</span>
          {guideIndex === null ? (
            <button className="secondary-action" data-active="true" onClick={() => setGuideIndex(0)} type="button"><Icon name="help" />{t('tourStartGuide')}</button>
          ) : null}
          <button className="secondary-action" onClick={onClose} type="button"><Icon name="back" />{t('tourBackToApp')}</button>
        </div>
      </header>

      <nav className="tour-surface-nav" aria-label={t('tourExploreTitle')}>
        <TourNavButton active={surface === 'wall'} icon="list" label={t('tourNavWall')} onClick={() => setSurface('wall')} />
        <TourNavButton active={surface === 'templates'} icon="templates" label={t('tourNavTemplates')} onClick={() => setSurface('templates')} />
        <TourNavButton active={surface === 'settings'} icon="settings" label={t('tourNavSettings')} onClick={() => setSurface('settings')} />
        <TourNavButton active={surface === 'account'} icon="user" label={t('tourNavAccount')} onClick={() => setSurface('account')} />
      </nav>

      {surface === 'wall' ? (
        <TourWall
          board={board}
          boards={boards}
          category={category}
          color={color}
          followUp={followUp}
          onBack={() => setSelectedTaskId(null)}
          onCategory={setCategory}
          onColor={setColor}
          onFollowUp={setFollowUp}
          onSearch={setSearch}
          onSelectBoard={selectBoard}
          onSelectTask={setSelectedTaskId}
          onStatus={setStatus}
          search={search}
          selectedTask={selectedTask}
          status={status}
          t={t}
          visibleTasks={visibleTasks}
        />
      ) : surface === 'templates' ? <TourTemplateStudio t={t} /> : surface === 'settings' ? <TourSettings t={t} /> : <TourAccount t={t} />}

      {guideIndex !== null ? (
        <GuidedTourOverlay
          currentIndex={guideIndex}
          onClose={dismissGuide}
          onNext={() => guideIndex === guideSteps.length - 1 ? dismissGuide() : setGuideIndex(guideIndex + 1)}
          onPrevious={() => setGuideIndex(Math.max(0, guideIndex - 1))}
          step={guideSteps[guideIndex]}
          stepCount={guideSteps.length}
          t={t}
        />
      ) : null}
    </section>
  );
}

function TourWall({ board, boards, category, color, followUp, onBack, onCategory, onColor, onFollowUp, onSearch, onSelectBoard, onSelectTask, onStatus, search, selectedTask, status, t, visibleTasks }: {
  board: ReturnType<typeof buildTourBoards>[number];
  boards: ReturnType<typeof buildTourBoards>;
  category: string;
  color: 'all' | TourColor;
  followUp: 'all' | Exclude<TourFollowUp, 'none'>;
  onBack: () => void;
  onCategory: (category: string) => void;
  onColor: (color: 'all' | TourColor) => void;
  onFollowUp: (followUp: 'all' | Exclude<TourFollowUp, 'none'>) => void;
  onSearch: (search: string) => void;
  onSelectBoard: (boardId: TourBoardId) => void;
  onSelectTask: (taskId: string) => void;
  onStatus: (status: string) => void;
  search: string;
  selectedTask: TourTask | null;
  status: string;
  t: Translate;
  visibleTasks: TourTask[];
}) {
  return (
    <div className="tour-workbench">
      <nav className="tour-board-switcher" aria-label={t('tourBoardsTitle')}>
        {boards.map((candidate) => (
          <button aria-current={candidate.id === board.id ? 'page' : undefined} key={candidate.id} onClick={() => onSelectBoard(candidate.id)} type="button">
            <span style={{ backgroundColor: candidate.color }} /><strong>{candidate.name}</strong>
            {candidate.members.length > 1 ? <span className="tour-shared-board-icon" title={t('tourSharedBoardTooltip')}><Icon name="users" /></span> : null}
          </button>
        ))}
      </nav>
      <section className="tour-wall" style={{ '--tour-board-color': board.color } as React.CSSProperties}>
        <header className="tour-wall-header">
          <div className="workspace-title-block">
            <div className="tour-wall-title-copy"><p className="detail-kicker">{t('tourExampleBoard')}</p><h2>{board.name}</h2><p>{board.story}</p></div>
            <TourMembers members={board.members} />
          </div>
          <span className="tour-static-label">{t('tourInteractiveHint')}</span>
        </header>
        <div className="tour-category-row" aria-label={t('category')}>
          <button aria-pressed={category === 'all'} onClick={() => onCategory('all')} type="button">{t('allProjects')}</button>
          {board.categories.map((candidate) => <button aria-pressed={category === candidate} key={candidate} onClick={() => onCategory(category === candidate ? 'all' : candidate)} type="button"><Icon name="tag" />{candidate}</button>)}
        </div>
        <div className="tour-filter-bar">
          <label><span className="sr-only">{t('tourFilterPlaceholder')}</span><Icon name="search" /><input onChange={(event) => onSearch(event.target.value)} placeholder={t('tourFilterPlaceholder')} type="search" value={search} /></label>
          <select aria-label={t('status')} onChange={(event) => onStatus(event.target.value)} value={status}><option value="all">{t('anyStatus')}</option>{[...new Set(board.tasks.map((task) => task.status))].map((option) => <option key={option} value={option}>{option}</option>)}</select>
          <div className="tour-color-filter" aria-label={t('color')}>
            <button aria-label={t('anyColor')} aria-pressed={color === 'all'} onClick={() => onColor('all')} type="button"><Icon name="filterOff" /></button>
            {(['yellow', 'blue', 'green', 'pink'] as const).map((option) => <button aria-label={t(`tourColor${capitalize(option)}` as Parameters<Translate>[0])} aria-pressed={color === option} data-color={option} key={option} onClick={() => onColor(color === option ? 'all' : option)} type="button" />)}
          </div>
          <select aria-label={t('followUp')} onChange={(event) => onFollowUp(event.target.value as typeof followUp)} value={followUp}><option value="all">{t('anyFollowUp')}</option><option value="soon">{t('tourDueSoon')}</option><option value="overdue">{t('tourOverdue')}</option></select>
        </div>
        {selectedTask ? <TourTaskDetail onBack={onBack} task={selectedTask} t={t} /> : (
          <div className="tour-task-grid">
            {visibleTasks.map((task) => <TourTaskCard key={task.id} onOpen={() => onSelectTask(task.id)} task={task} t={t} />)}
            {visibleTasks.length === 0 ? <p className="tour-empty-state">{t('tourNoResults')}</p> : null}
          </div>
        )}
      </section>
    </div>
  );
}

function TourTaskCard({ onOpen, task, t }: { onOpen: () => void; task: TourTask; t: Translate }) {
  const completed = task.entries.filter((entry) => entry.done).length;
  return (
    <button className="tour-task-card" data-color={task.color} onClick={onOpen} type="button">
      <span className="tour-task-topline"><strong>{task.title}</strong><span className="tour-task-indicators">{task.members.length > 1 ? <small className="note-count" title={`${t('sharing')}: ${task.members.map((member) => member.name).join(', ')}`}><Icon name="users" />{task.members.length}</small> : null}<small>{task.updated}</small></span></span>
      <span className="tour-task-note">{task.note}</span>
      {task.template === t('tourTodoTemplate') ? <span className="tour-task-checklist"><Icon name="check" />{completed}/{task.entries.length} {t('tourItemsDone')}</span> : null}
      <span className="tour-task-meta"><span><Icon name="status" />{task.status}</span><span><Icon name="tag" />{task.category}</span>{task.followUp !== 'none' ? <span data-overdue={task.followUp === 'overdue'}><Icon name="calendarX" />{task.followUpLabel}</span> : null}</span>
      <span className="tour-task-open">{t('tourOpenTask')} <Icon name="back" /></span>
    </button>
  );
}

function TourTaskDetail({ onBack, task, t }: { onBack: () => void; task: TourTask; t: Translate }) {
  return (
    <section className="tour-task-detail" data-color={task.color}>
      <header><button aria-label={t('backToWall')} onClick={onBack} type="button"><Icon name="back" /></button><div><p className="detail-kicker">{task.template}</p><h3>{task.title}</h3></div><TourMembers members={task.members} /></header>
      <div className="tour-task-description"><small>{t('tourDescriptionLabel')}</small><p>{task.description}</p></div>
      <div className="tour-task-detail-fields"><span><small>{t('status')}</small><strong>{task.status}</strong></span><span><small>{t('category')}</small><strong>{task.category}</strong></span><span><small>{t('followUp')}</small><strong>{task.followUpLabel || t('noFollowUp')}</strong></span></div>
      <div className="tour-task-detail-notes"><header><h4>{t('notes')}</h4><span>{task.entries.length} {t('tourItems')}</span></header>{task.entries.map((entry) => <div className="tour-entry-row" key={entry.id}><span className="tour-entry-check" data-done={entry.done}>{entry.done ? <Icon name="check" /> : null}</span><span><strong>{entry.text}</strong><small>{entry.author} · {entry.time}</small></span></div>)}</div>
    </section>
  );
}

function TourSettings({ t }: { t: Translate }) {
  return <section className="tour-example-panel"><header><Icon name="settings" /><div><p className="detail-kicker">{t('tourNavSettings')}</p><h2>{t('tourSettingsTitle')}</h2><p>{t('tourSettingsIntro')}</p></div></header><div className="tour-setting-row"><div><strong>{t('tourStatusSettings')}</strong><p>{t('tourStatusSettingsHelp')}</p></div><span>{t('tourStatusActive')}</span><span>{t('tourStatusWaiting')}</span><span>{t('tourStatusDone')}</span></div><p className="tour-example-note"><Icon name="shield" />{t('tourExamplesOnly')}</p></section>;
}

function TourAccount({ t }: { t: Translate }) {
  return <section className="tour-example-panel"><header><Icon name="user" /><div><p className="detail-kicker">{t('tourNavAccount')}</p><h2>{t('tourAccountTitle')}</h2><p>{t('tourAccountIntro')}</p></div></header><div className="tour-account-status"><span className="tour-local-dot" /> <div><strong>{t('tourLocalFirst')}</strong><p>{t('tourLocalFirstHelp')}</p></div></div><div className="tour-account-status"><Icon name="cloud" /><div><strong>{t('tourCloudOptional')}</strong><p>{t('tourCloudOptionalHelp')}</p></div></div><div className="tour-account-status"><Icon name="users" /><div><strong>{t('tourSharingTitle')}</strong><p>{t('tourSharingHelp')}</p></div></div></section>;
}

function TourMembers({ members }: { members: TourMember[] }) {
  return <span className="member-chip-strip tour-member-list">{members.map((member) => <span className={`member-chip ${member.id === 'bjarke' ? 'member-chip-owner' : ''}`} key={member.id} title={`${member.name} · ${member.role}`}><Icon name={member.kind === 'group' ? 'users' : member.id === 'bjarke' ? 'crown' : 'user'} />{member.name}</span>)}</span>;
}

function TourNavButton({ active, icon, label, onClick }: { active: boolean; icon: 'list' | 'templates' | 'settings' | 'user'; label: string; onClick: () => void }) {
  return <button aria-current={active ? 'page' : undefined} onClick={onClick} type="button"><Icon name={icon} />{label}</button>;
}

function capitalize(value: string) { return `${value.charAt(0).toUpperCase()}${value.slice(1)}`; }
