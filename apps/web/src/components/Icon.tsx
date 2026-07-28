export type IconName =
  | 'archive'
  | 'arrowDown'
  | 'arrowUp'
  | 'back'
  | 'check'
  | 'calendarX'
  | 'cloud'
  | 'chevronDown'
  | 'clock'
  | 'close'
  | 'crown'
  | 'edit'
  | 'filterOff'
  | 'inbox'
  | 'list'
  | 'login'
  | 'logout'
  | 'mail'
  | 'minus'
  | 'note'
  | 'palette'
  | 'panel'
  | 'plus'
  | 'refresh'
  | 'search'
  | 'settings'
  | 'shield'
  | 'status'
  | 'tag'
  | 'templates'
  | 'trash'
  | 'undo'
  | 'user'
  | 'userPlus'
  | 'users'
  | 'waiting';

const iconPaths: Record<IconName, string> = {
  archive: 'M4 7h16v13H4V7Zm2-4h12l2 4H4l2-4Zm5 8h2',
  arrowDown: 'M12 5v14m0 0 6-6m-6 6-6-6',
  arrowUp: 'M12 19V5m0 0 6 6m-6-6-6 6',
  back: 'M15 6 9 12l6 6M10 12h10',
  calendarX: 'M7 3v4M17 3v4M4 9h16M6 5h12a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Zm6 8 4 4m0-4-4 4',
  check: 'm5 13 4 4L19 7',
  chevronDown: 'm6 9 6 6 6-6',
  cloud: 'M17 18H8a5 5 0 1 1 .9-9.9A6.5 6.5 0 0 1 21 11.5 3.5 3.5 0 0 1 17 18Z',
  clock: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16Zm0 4v5l3 2',
  close: 'M6 6l12 12M18 6 6 18',
  crown: 'M5 17h14l1-9-5 4-3-6-3 6-5-4 1 9Zm1 3h12',
  edit: 'M4 20h4l10-10-4-4L4 16v4Zm12-16 4 4',
  filterOff: 'M4 5h16l-6 7v4l-4 2v-6L4 5M3 3l18 18',
  inbox: 'M4 5h16v10l-3 4H7l-3-4V5Zm0 10h5l1.5 2h3L15 15h5',
  list: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
  login: 'M10 17l5-5-5-5M15 12H3M21 5v14a2 2 0 0 1-2 2h-5M14 3h5a2 2 0 0 1 2 2',
  logout: 'M14 7l-5 5 5 5M9 12h12M3 5v14a2 2 0 0 0 2 2h5M10 3H5a2 2 0 0 0-2 2',
  mail: 'M4 6h16v12H4V6Zm0 2 8 5 8-5',
  minus: 'M5 12h14',
  note: 'M5 4h11l3 3v13H5V4Zm11 0v4h4M8 12h8M8 16h6',
  palette: 'M12 4a8 8 0 0 0-1 15.94c.8.1 1.33-.55 1.14-1.33-.13-.55.28-1.04.85-1.04h1.36A5.65 5.65 0 0 0 20 11.92C20 7.55 16.42 4 12 4ZM8 11.5h.01M10 8h.01M14 8h.01M16 11h.01',
  panel: 'M4 5h16v14H4V5Zm5 0v14',
  plus: 'M12 5v14M5 12h14',
  refresh: 'M20 7v5h-5M4 17v-5h5M18 10a6 6 0 0 0-10-4L4 10m2 4a6 6 0 0 0 10 4l4-4',
  search: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14Zm5 12 4 4',
  settings: 'M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.09a2 2 0 0 1 1 1.74v.5a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.38a2 2 0 0 0-.73-2.73l-.15-.09a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2ZM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z',
  shield: 'M12 3 20 6v6c0 5-3.4 8-8 9-4.6-1-8-4-8-9V6l8-3Zm-3 9 2 2 4-5',
  status: 'M5 7h14M5 12h14M5 17h9',
  tag: 'M20 10 14 4H5v9l6 6 9-9ZM8 8h.01',
  templates: 'M4 5h7v7H4V5Zm9 0h7v7h-7V5ZM4 14h7v5H4v-5Zm9 0h7v5h-7v-5Z',
  trash: 'M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3',
  undo: 'M9 7 4 12l5 5M4 12h10a5 5 0 1 1-3.5 8.5',
  user: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm-7 8a7 7 0 0 1 14 0',
  userPlus: 'M9 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm-6 8a7 7 0 0 1 12 0M18 9v6m-3-3h6',
  users: 'M9 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm-6 8a7 7 0 0 1 12 0M17 11a3 3 0 1 0 0-6M15 20a5 5 0 0 1 7-4.5',
  waiting: 'M6 4h12M8 4v5l4 3 4-3V4M8 20v-5l4-3 4 3v5M6 20h12',
};

export function Icon({ name }: { name: IconName }) {
  return (
    <svg
      aria-hidden="true"
      className="icon"
      fill="none"
      focusable="false"
      viewBox="0 0 24 24"
    >
      <path
        d={iconPaths[name]}
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  );
}
