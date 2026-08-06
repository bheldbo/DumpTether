import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';
import type { TourGuideStep } from './tourData';

export function GuidedTourOverlay({
  currentIndex,
  onClose,
  onNext,
  onPrevious,
  step,
  stepCount,
  t,
}: {
  currentIndex: number;
  onClose: () => void;
  onNext: () => void;
  onPrevious: () => void;
  step: TourGuideStep;
  stepCount: number;
  t: Translate;
}) {
  const isLast = currentIndex === stepCount - 1;

  return (
    <aside aria-live="polite" className="tour-guide-bubble">
      <div className="tour-guide-heading">
        <span>{t('tourGuideStep')} {currentIndex + 1}/{stepCount}</span>
        <button aria-label={t('tourGuideSkip')} onClick={onClose} title={t('tourGuideSkip')} type="button">
          <Icon name="close" />
        </button>
      </div>
      <h2>{step.title}</h2>
      <p>{step.body}</p>
      <div className="tour-guide-progress" aria-hidden="true">
        {Array.from({ length: stepCount }, (_, index) => <span data-active={index <= currentIndex} key={index} />)}
      </div>
      <div className="tour-guide-actions">
        <button disabled={currentIndex === 0} onClick={onPrevious} type="button">
          <Icon name="back" />
          {t('tourGuidePrevious')}
        </button>
        <button className="primary-action" onClick={onNext} type="button">
          {isLast ? t('tourGuideDone') : t('tourGuideNext')}
          {!isLast ? <Icon name="back" /> : <Icon name="check" />}
        </button>
      </div>
    </aside>
  );
}
