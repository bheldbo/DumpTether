import { useMemo, useState } from 'react';
import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';

type TourBoardId = 'work' | 'home' | 'vacation';
type TourColor = 'yellow' | 'blue' | 'green' | 'pink';
type TourFollowUp = 'none' | 'soon' | 'overdue';

interface TourTask {
  id: string;
  title: string;
  note: string;
  category: string;
  status: string;
  color: TourColor;
  followUp: TourFollowUp;
  followUpLabel: string;
  updated: string;
  template: string;
  entries: string[];
}

interface TourBoard {
  id: TourBoardId;
  name: string;
  story: string;
  color: string;
  categories: string[];
  tasks: TourTask[];
}

export function ProductTourPage({
  onClose,
  t,
}: {
  onClose: () => void;
  t: Translate;
}) {
  const boards = useMemo(() => buildTourBoards(t), [t]);
  const [boardId, setBoardId] = useState<TourBoardId>('work');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [color, setColor] = useState<'all' | TourColor>('all');
  const [followUp, setFollowUp] = useState<'all' | Exclude<TourFollowUp, 'none'>>('all');
  const [category, setCategory] = useState('all');
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const board = boards.find((candidate) => candidate.id === boardId) ?? boards[0];
  const normalizedSearch = search.trim().toLocaleLowerCase();
  const visibleTasks = board.tasks.filter((task) =>
    (normalizedSearch.length === 0 ||
      `${task.title} ${task.note} ${task.category} ${task.status}`
        .toLocaleLowerCase()
        .includes(normalizedSearch)) &&
    (status === 'all' || task.status === status) &&
    (color === 'all' || task.color === color) &&
    (followUp === 'all' || task.followUp === followUp) &&
    (category === 'all' || task.category === category));
  const selectedTask = board.tasks.find((task) => task.id === selectedTaskId) ?? null;

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
          <button className="secondary-action" onClick={onClose} type="button">
            <Icon name="back" />
            {t('tourBackToApp')}
          </button>
        </div>
      </header>

      <div className="tour-concept-band" aria-label={t('tourConcepts')}>
        <p><strong>{t('board')}</strong><span>{t('tourBoardConcept')}</span></p>
        <p><strong>{t('templates')}</strong><span>{t('tourTemplateConcept')}</span></p>
        <p><strong>{t('category')}</strong><span>{t('tourCategoryConcept')}</span></p>
      </div>

      <div className="tour-workbench">
        <nav className="tour-board-switcher" aria-label={t('tourBoardsTitle')}>
          {boards.map((candidate) => (
            <button
              aria-current={candidate.id === board.id ? 'page' : undefined}
              key={candidate.id}
              onClick={() => selectBoard(candidate.id)}
              type="button"
            >
              <span style={{ backgroundColor: candidate.color }} />
              <strong>{candidate.name}</strong>
            </button>
          ))}
        </nav>

        <section className="tour-wall" style={{ '--tour-board-color': board.color } as React.CSSProperties}>
          <header className="tour-wall-header">
            <div>
              <p className="detail-kicker">{t('tourExampleBoard')}</p>
              <h2>{board.name}</h2>
              <p>{board.story}</p>
            </div>
            <span className="tour-static-label">{t('tourInteractiveHint')}</span>
          </header>

          <div className="tour-category-row" aria-label={t('category')}>
            <button
              aria-pressed={category === 'all'}
              onClick={() => setCategory('all')}
              type="button"
            >
              {t('allProjects')}
            </button>
            {board.categories.map((candidate) => (
              <button
                aria-pressed={category === candidate}
                key={candidate}
                onClick={() => setCategory((current) => current === candidate ? 'all' : candidate)}
                type="button"
              >
                <Icon name="tag" />
                {candidate}
              </button>
            ))}
          </div>

          <div className="tour-filter-bar">
            <label>
              <span className="sr-only">{t('tourFilterPlaceholder')}</span>
              <Icon name="search" />
              <input
                onChange={(event) => setSearch(event.target.value)}
                placeholder={t('tourFilterPlaceholder')}
                type="search"
                value={search}
              />
            </label>
            <select aria-label={t('status')} onChange={(event) => setStatus(event.target.value)} value={status}>
              <option value="all">{t('anyStatus')}</option>
              {[...new Set(board.tasks.map((task) => task.status))].map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
            <div className="tour-color-filter" aria-label={t('color')}>
              <button
                aria-label={t('anyColor')}
                aria-pressed={color === 'all'}
                onClick={() => setColor('all')}
                type="button"
              >
                <Icon name="filterOff" />
              </button>
              {(['yellow', 'blue', 'green', 'pink'] as const).map((option) => (
                <button
                  aria-label={t(`tourColor${capitalize(option)}` as Parameters<Translate>[0])}
                  aria-pressed={color === option}
                  data-color={option}
                  key={option}
                  onClick={() => setColor((current) => current === option ? 'all' : option)}
                  type="button"
                />
              ))}
            </div>
            <select
              aria-label={t('followUp')}
              onChange={(event) => setFollowUp(event.target.value as typeof followUp)}
              value={followUp}
            >
              <option value="all">{t('anyFollowUp')}</option>
              <option value="soon">{t('tourDueSoon')}</option>
              <option value="overdue">{t('tourOverdue')}</option>
            </select>
          </div>

          {selectedTask ? (
            <TourTaskDetail
              onBack={() => setSelectedTaskId(null)}
              task={selectedTask}
              t={t}
            />
          ) : (
            <div className="tour-task-grid">
              {visibleTasks.map((task) => (
                <button
                  className="tour-task-card"
                  data-color={task.color}
                  key={task.id}
                  onClick={() => setSelectedTaskId(task.id)}
                  type="button"
                >
                  <span className="tour-task-topline">
                    <strong>{task.title}</strong>
                    <small>{task.updated}</small>
                  </span>
                  <span className="tour-task-note">{task.note}</span>
                  <span className="tour-task-meta">
                    <span><Icon name="status" />{task.status}</span>
                    <span><Icon name="tag" />{task.category}</span>
                    {task.followUp !== 'none' ? (
                      <span data-overdue={task.followUp === 'overdue'}>
                        <Icon name="calendarX" />{task.followUpLabel}
                      </span>
                    ) : null}
                  </span>
                  <span className="tour-task-open">{t('tourOpenTask')} <Icon name="back" /></span>
                </button>
              ))}
              {visibleTasks.length === 0 ? (
                <p className="tour-empty-state">{t('tourNoResults')}</p>
              ) : null}
            </div>
          )}
        </section>
      </div>
    </section>
  );
}

function TourTaskDetail({
  onBack,
  task,
  t,
}: {
  onBack: () => void;
  task: TourTask;
  t: Translate;
}) {
  return (
    <section className="tour-task-detail" data-color={task.color}>
      <header>
        <button aria-label={t('backToWall')} onClick={onBack} type="button"><Icon name="back" /></button>
        <div><p className="detail-kicker">{task.template}</p><h3>{task.title}</h3></div>
      </header>
      <div className="tour-task-detail-fields">
        <span><small>{t('status')}</small><strong>{task.status}</strong></span>
        <span><small>{t('category')}</small><strong>{task.category}</strong></span>
        <span><small>{t('followUp')}</small><strong>{task.followUpLabel || t('noFollowUp')}</strong></span>
      </div>
      <div className="tour-task-detail-notes">
        <h4>{t('notes')}</h4>
        {task.entries.map((entry, index) => (
          <p key={`${task.id}:${index}`}><time>{index === 0 ? t('tourToday') : t('tourYesterday')}</time><span>{entry}</span></p>
        ))}
      </div>
    </section>
  );
}

function buildTourBoards(t: Translate): TourBoard[] {
  return [
    {
      id: 'work',
      name: t('tourWorkBoard'),
      story: t('tourWorkStory'),
      color: '#69c9bc',
      categories: [t('tourConferenceCategory'), t('tourOperationsCategory')],
      tasks: [
        tourTask('poster', t('tourPosterTitle'), t('tourPosterNote'), t('tourConferenceCategory'), t('tourStatusWaiting'), 'blue', 'soon', t('tourFriday'), '2h', t('tourWorkTemplate'), [t('tourPosterEntryOne'), t('tourPosterEntryTwo')]),
        tourTask('catering', t('tourCateringTitle'), t('tourCateringNote'), t('tourConferenceCategory'), t('tourStatusActive'), 'yellow', 'overdue', t('tourYesterday'), '5h', t('tourTodoTemplate'), [t('tourCateringEntryOne'), t('tourCateringEntryTwo')]),
        tourTask('handover', t('tourHandoverTitle'), t('tourHandoverNote'), t('tourOperationsCategory'), t('tourStatusDone'), 'green', 'none', '', '1d', t('tourBasicTemplate'), [t('tourHandoverEntry')]),
      ],
    },
    {
      id: 'home',
      name: t('tourHomeBoard'),
      story: t('tourHomeStory'),
      color: '#f2ca67',
      categories: [t('tourHouseCategory'), t('tourShoppingCategory')],
      tasks: [
        tourTask('bulb', t('tourBulbTitle'), t('tourBulbNote'), t('tourHouseCategory'), t('tourStatusActive'), 'yellow', 'soon', t('tourSaturday'), '3h', t('tourTodoTemplate'), [t('tourBulbEntryOne'), t('tourBulbEntryTwo')]),
        tourTask('reset', t('tourResetTitle'), t('tourResetNote'), t('tourHouseCategory'), t('tourStatusWaiting'), 'pink', 'none', '', '1d', t('tourTodoTemplate'), [t('tourResetEntryOne'), t('tourResetEntryTwo')]),
      ],
    },
    {
      id: 'vacation',
      name: t('tourVacationBoard'),
      story: t('tourVacationStory'),
      color: '#8ebaf0',
      categories: [t('tourTravelCategory'), t('tourPackingCategory')],
      tasks: [
        tourTask('train', t('tourTrainTitle'), t('tourTrainNote'), t('tourTravelCategory'), t('tourStatusBooked'), 'blue', 'none', '', '4h', t('tourBasicTemplate'), [t('tourTrainEntry')]),
        tourTask('packing', t('tourPackingTitle'), t('tourPackingNote'), t('tourPackingCategory'), t('tourStatusActive'), 'green', 'soon', t('tourNextWeek'), '8h', t('tourTodoTemplate'), [t('tourPackingEntryOne'), t('tourPackingEntryTwo')]),
      ],
    },
  ];
}

function tourTask(
  id: string,
  title: string,
  note: string,
  category: string,
  status: string,
  color: TourColor,
  followUp: TourFollowUp,
  followUpLabel: string,
  updated: string,
  template: string,
  entries: string[],
): TourTask {
  return { id, title, note, category, status, color, followUp, followUpLabel, updated, template, entries };
}

function capitalize(value: string) {
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}
