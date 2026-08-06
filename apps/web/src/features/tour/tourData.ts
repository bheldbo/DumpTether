import type { Translate } from '../../localization';

export type TourSurface = 'wall' | 'templates' | 'settings' | 'account';
export type TourBoardId = 'work' | 'home' | 'vacation';
export type TourColor = 'yellow' | 'blue' | 'green' | 'pink';
export type TourFollowUp = 'none' | 'soon' | 'overdue';

export interface TourMember {
  id: string;
  name: string;
  role: string;
  kind: 'person' | 'group';
}

export interface TourEntry {
  id: string;
  text: string;
  author: string;
  time: string;
  done?: boolean;
}

export interface TourTask {
  id: string;
  title: string;
  note: string;
  description: string;
  category: string;
  status: string;
  color: TourColor;
  followUp: TourFollowUp;
  followUpLabel: string;
  updated: string;
  template: string;
  members: TourMember[];
  entries: TourEntry[];
}

export interface TourBoard {
  id: TourBoardId;
  name: string;
  story: string;
  color: string;
  categories: string[];
  members: TourMember[];
  tasks: TourTask[];
}

export interface TourGuideStep {
  id: string;
  surface: TourSurface;
  boardId?: TourBoardId;
  taskId?: string;
  title: string;
  body: string;
}

export function buildTourBoards(t: Translate): TourBoard[] {
  const bjarke = person('bjarke', t('tourBjarke'), t('tourOwner'));
  const frederikke = person('frederikke', t('tourFrederikke'), t('tourMember'));
  const conferenceCrew = group('conference-crew', t('tourConferenceCrew'), t('tourGroup'));

  return [
    {
      id: 'work',
      name: t('tourWorkBoard'),
      story: t('tourWorkStory'),
      color: '#69c9bc',
      categories: [t('tourConferenceCategory'), t('tourOperationsCategory')],
      members: [bjarke, frederikke, conferenceCrew],
      tasks: [
        task('poster', t('tourPosterTitle'), t('tourPosterNote'), t('tourPosterDescription'), t('tourConferenceCategory'), t('tourStatusWaiting'), 'blue', 'soon', t('tourFriday'), '2h', t('tourWorkTemplate'), [bjarke, conferenceCrew], [
          entry('poster-1', t('tourPosterEntryOne'), t('tourBjarke'), t('tourToday')),
          entry('poster-2', t('tourPosterEntryTwo'), t('tourConferenceCrew'), t('tourToday')),
        ]),
        task('catering', t('tourCateringTitle'), t('tourCateringNote'), t('tourCateringDescription'), t('tourConferenceCategory'), t('tourStatusActive'), 'yellow', 'overdue', t('tourYesterday'), '5h', t('tourTodoTemplate'), [frederikke, conferenceCrew], [
          entry('catering-1', t('tourCateringEntryOne'), t('tourFrederikke'), t('tourYesterday'), true),
          entry('catering-2', t('tourCateringEntryTwo'), t('tourBjarke'), t('tourToday')),
        ]),
        task('schedule', t('tourScheduleTitle'), t('tourScheduleNote'), t('tourScheduleDescription'), t('tourConferenceCategory'), t('tourStatusActive'), 'pink', 'soon', t('tourFriday'), '7h', t('tourTodoTemplate'), [bjarke, frederikke], [
          entry('schedule-1', t('tourScheduleEntryOne'), t('tourBjarke'), t('tourYesterday'), true),
          entry('schedule-2', t('tourScheduleEntryTwo'), t('tourFrederikke'), t('tourToday')),
        ]),
        task('handover', t('tourHandoverTitle'), t('tourHandoverNote'), t('tourHandoverDescription'), t('tourOperationsCategory'), t('tourStatusDone'), 'green', 'none', '', '1d', t('tourBasicTemplate'), [bjarke], [
          entry('handover-1', t('tourHandoverEntry'), t('tourBjarke'), t('tourYesterday')),
        ]),
      ],
    },
    {
      id: 'home',
      name: t('tourHomeBoard'),
      story: t('tourHomeStory'),
      color: '#f2ca67',
      categories: [t('tourHouseCategory'), t('tourShoppingCategory')],
      members: [bjarke, frederikke],
      tasks: [
        task('bulb', t('tourBulbTitle'), t('tourBulbNote'), t('tourBulbDescription'), t('tourHouseCategory'), t('tourStatusActive'), 'yellow', 'soon', t('tourSaturday'), '3h', t('tourTodoTemplate'), [bjarke], [
          entry('bulb-1', t('tourBulbEntryOne'), t('tourBjarke'), t('tourYesterday'), true),
          entry('bulb-2', t('tourBulbEntryTwo'), t('tourBjarke'), t('tourToday')),
        ]),
        task('reset', t('tourResetTitle'), t('tourResetNote'), t('tourResetDescription'), t('tourHouseCategory'), t('tourStatusWaiting'), 'pink', 'none', '', '1d', t('tourTodoTemplate'), [bjarke, frederikke], [
          entry('reset-1', t('tourResetEntryOne'), t('tourFrederikke'), t('tourToday'), true),
          entry('reset-2', t('tourResetEntryTwo'), t('tourBjarke'), t('tourToday')),
        ]),
      ],
    },
    {
      id: 'vacation',
      name: t('tourVacationBoard'),
      story: t('tourVacationStory'),
      color: '#8ebaf0',
      categories: [t('tourTravelCategory'), t('tourPackingCategory')],
      members: [bjarke, frederikke],
      tasks: [
        task('train', t('tourTrainTitle'), t('tourTrainNote'), t('tourTrainDescription'), t('tourTravelCategory'), t('tourStatusBooked'), 'blue', 'none', '', '4h', t('tourBasicTemplate'), [bjarke, frederikke], [
          entry('train-1', t('tourTrainEntry'), t('tourBjarke'), t('tourToday')),
        ]),
        packingTask('packing-bjarke', t('tourPackingBjarkeTitle'), t('tourPackingBjarkeNote'), bjarke, 'green', t),
        packingTask('packing-frederikke', t('tourPackingFrederikkeTitle'), t('tourPackingFrederikkeNote'), frederikke, 'pink', t),
        task('packing-together', t('tourPackingTogetherTitle'), t('tourPackingTogetherNote'), t('tourPackingTogetherDescription'), t('tourPackingCategory'), t('tourStatusActive'), 'yellow', 'soon', t('tourNextWeek'), '12h', t('tourTodoTemplate'), [bjarke, frederikke], [
          entry('packing-together-1', t('tourPackingTogetherEntryOne'), t('tourFrederikke'), t('tourToday'), true),
          entry('packing-together-2', t('tourPackingTogetherEntryTwo'), t('tourBjarke'), t('tourToday')),
        ]),
      ],
    },
  ];
}

export function buildTourGuideSteps(t: Translate): TourGuideStep[] {
  return [
    { id: 'welcome', surface: 'wall', boardId: 'work', title: t('tourStepWelcomeTitle'), body: t('tourStepWelcomeBody') },
    { id: 'boards', surface: 'wall', boardId: 'work', title: t('tourStepBoardsTitle'), body: t('tourStepBoardsBody') },
    { id: 'sharing', surface: 'wall', boardId: 'work', title: t('tourStepSharingTitle'), body: t('tourStepSharingBody') },
    { id: 'template', surface: 'templates', title: t('tourStepTemplateTitle'), body: t('tourStepTemplateBody') },
    { id: 'packing', surface: 'wall', boardId: 'vacation', taskId: 'packing-together', title: t('tourStepPackingTitle'), body: t('tourStepPackingBody') },
    { id: 'filters', surface: 'wall', boardId: 'work', title: t('tourStepFiltersTitle'), body: t('tourStepFiltersBody') },
    { id: 'account', surface: 'account', title: t('tourStepAccountTitle'), body: t('tourStepAccountBody') },
  ];
}

function packingTask(id: string, title: string, note: string, member: TourMember, color: TourColor, t: Translate): TourTask {
  return task(id, title, note, t('tourPackingDescription'), t('tourPackingCategory'), t('tourStatusActive'), color, 'soon', t('tourNextWeek'), '8h', t('tourTodoTemplate'), [member], [
    entry(`${id}-1`, t('tourPackingEntryOne'), member.name, t('tourToday'), true),
    entry(`${id}-2`, t('tourPackingEntryTwo'), member.name, t('tourToday')),
    entry(`${id}-3`, t('tourPackingEntryThree'), member.name, t('tourToday')),
  ]);
}

function task(
  id: string,
  title: string,
  note: string,
  description: string,
  category: string,
  status: string,
  color: TourColor,
  followUp: TourFollowUp,
  followUpLabel: string,
  updated: string,
  template: string,
  members: TourMember[],
  entries: TourEntry[],
): TourTask {
  return { id, title, note, description, category, status, color, followUp, followUpLabel, updated, template, members, entries };
}

function entry(id: string, text: string, author: string, time: string, done = false): TourEntry {
  return { id, text, author, time, done };
}

function person(id: string, name: string, role: string): TourMember {
  return { id, name, role, kind: 'person' };
}

function group(id: string, name: string, role: string): TourMember {
  return { id, name, role, kind: 'group' };
}
