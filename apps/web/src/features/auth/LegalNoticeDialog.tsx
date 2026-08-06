import { Icon } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import type { Translate } from '../../localization';
import type { LegalClientOptionsResponse } from '../../types';

export type LegalDocumentKind = 'terms' | 'privacy';

export function LegalNoticeDialog({
  kind,
  legal,
  onClose,
  t,
}: {
  kind: LegalDocumentKind;
  legal: LegalClientOptionsResponse;
  onClose: () => void;
  t: Translate;
}) {
  const isTerms = kind === 'terms';
  const version = isTerms ? legal.termsVersion : legal.privacyNoticeVersion;

  return (
    <ModalFrame onClose={onClose}>
      <article
        aria-labelledby="legal-notice-title"
        aria-modal="true"
        className="legal-notice-dialog"
        role="dialog"
      >
        <header className="dialog-header">
          <div>
            <p className="detail-kicker">DumpTether</p>
            <h2 id="legal-notice-title">
              {isTerms ? t('termsOfUse') : t('privacyNotice')}
            </h2>
            {version ? <p>{t('legalVersion')}: {version}</p> : null}
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </header>

        {isTerms ? (
          <div className="legal-notice-content">
            <LegalSection title={t('termsServiceTitle')} body={t('termsServiceBody')} />
            <LegalSection title={t('termsContentTitle')} body={t('termsContentBody')} />
            <LegalSection title={t('termsAvailabilityTitle')} body={t('termsAvailabilityBody')} />
            <LegalSection title={t('termsAcceptableUseTitle')} body={t('termsAcceptableUseBody')} />
            <LegalSection title={t('termsLiabilityTitle')} body={t('termsLiabilityBody')} />
            <LegalSection title={t('termsChangesTitle')} body={t('termsChangesBody')} />
          </div>
        ) : (
          <div className="legal-notice-content">
            <LegalSection
              title={t('privacyControllerTitle')}
              body={`${legal.operatorName || t('legalOperatorNotConfigured')} - ${legal.privacyContactEmail || t('legalContactNotConfigured')}`}
            />
            <LegalSection title={t('privacyDataTitle')} body={t('privacyDataBody')} />
            <LegalSection title={t('privacyPurposeTitle')} body={t('privacyPurposeBody')} />
            <LegalSection title={t('privacyProvidersTitle')} body={t('privacyProvidersBody')} />
            <LegalSection title={t('privacyRetentionTitle')} body={t('privacyRetentionBody')} />
            <LegalSection title={t('privacyRightsTitle')} body={t('privacyRightsBody')} />
            <LegalSection title={t('privacyCookiesTitle')} body={t('privacyCookiesBody')} />
          </div>
        )}
      </article>
    </ModalFrame>
  );
}

function LegalSection({ title, body }: { title: string; body: string }) {
  return (
    <section>
      <h3>{title}</h3>
      <p>{body}</p>
    </section>
  );
}

export function MicrosoftMark() {
  return (
    <span aria-hidden="true" className="microsoft-mark">
      <span />
      <span />
      <span />
      <span />
    </span>
  );
}
