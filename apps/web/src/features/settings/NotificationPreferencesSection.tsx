import { useEffect, useState } from 'react';
import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';
import type {
  AccountNotificationPreferencesResponse,
  UpdateAccountNotificationPreferencesRequest,
} from '../../types';

type PreferenceKey = keyof UpdateAccountNotificationPreferencesRequest;

export function NotificationPreferencesSection({
  preferences,
  onUpdate,
  t,
}: {
  preferences: AccountNotificationPreferencesResponse;
  onUpdate: (
    request: UpdateAccountNotificationPreferencesRequest,
  ) => Promise<AccountNotificationPreferencesResponse>;
  t: Translate;
}) {
  const [draft, setDraft] = useState(preferences);
  const [savingKey, setSavingKey] = useState<PreferenceKey | null>(null);

  useEffect(() => setDraft(preferences), [preferences]);

  const toggle = async (key: PreferenceKey) => {
    const next = { ...draft, [key]: !draft[key] };
    setDraft(next);
    setSavingKey(key);
    try {
      setDraft(await onUpdate({
        sharingActivityEmailEnabled: next.sharingActivityEmailEnabled,
        dailySummaryEmailEnabled: next.dailySummaryEmailEnabled,
        followUpReminderEmailEnabled: next.followUpReminderEmailEnabled,
      }));
    } catch {
      setDraft(preferences);
    } finally {
      setSavingKey(null);
    }
  };

  const options: Array<{
    key: PreferenceKey;
    title: Parameters<Translate>[0];
    help: Parameters<Translate>[0];
  }> = [
    {
      key: 'sharingActivityEmailEnabled',
      title: 'sharingEmailNotifications',
      help: 'sharingEmailNotificationsHelp',
    },
    {
      key: 'dailySummaryEmailEnabled',
      title: 'dailySummaryEmail',
      help: 'dailySummaryEmailHelp',
    },
    {
      key: 'followUpReminderEmailEnabled',
      title: 'followUpEmailNotifications',
      help: 'followUpEmailNotificationsHelp',
    },
  ];

  return (
    <section className="settings-section notification-preferences-section">
      <div className="section-heading-with-icon">
        <Icon name="mail" />
        <div>
          <h3>{t('emailNotifications')}</h3>
          <p>{t('emailNotificationsHelp')}</p>
        </div>
      </div>
      {!draft.emailDeliveryAvailable ? (
        <p className="form-note">{t('emailNotificationsUnavailable')}</p>
      ) : null}
      <div className="notification-preference-list">
        {options.map((option) => (
          <label className="notification-preference-row" key={option.key}>
            <span>
              <strong>{t(option.title)}</strong>
              <small>{t(option.help)}</small>
            </span>
            <input
              checked={draft[option.key]}
              disabled={!draft.emailDeliveryAvailable || savingKey !== null}
              onChange={() => void toggle(option.key)}
              type="checkbox"
            />
          </label>
        ))}
      </div>
    </section>
  );
}
