import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';

export function HelpSection({
  onStartTour,
  t,
}: {
  onStartTour: () => void;
  t: Translate;
}) {
  return (
    <section className="settings-section account-help-section">
      <h3>{t('help')}</h3>
      <p>{t('accountHelpIntro')}</p>
      <button className="secondary-action" onClick={onStartTour} type="button">
        <Icon name="help" />
        {t('startTutorial')}
      </button>

      <div className="account-faq">
        <h4>{t('faq')}</h4>
        <details>
          <summary>{t('faqTasksQuestion')}</summary>
          <p>{t('faqTasksAnswer')}</p>
        </details>
        <details>
          <summary>{t('faqTemplatesQuestion')}</summary>
          <p>{t('faqTemplatesAnswer')}</p>
        </details>
        <details>
          <summary>{t('faqArchiveQuestion')}</summary>
          <p>{t('faqArchiveAnswer')}</p>
        </details>
      </div>
    </section>
  );
}
