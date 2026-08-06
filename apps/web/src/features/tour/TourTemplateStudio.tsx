import { useState } from 'react';
import { Icon } from '../../components/Icon';
import { ModalFrame } from '../../components/ModalFrame';
import type { Translate } from '../../localization';

type CellId = 'header-description' | 'entry-item' | 'entry-done';
type TourFieldType = 'longtext' | 'text' | 'checkbox';

interface TourFieldDefinition {
  id: CellId;
  label: string;
  type: TourFieldType;
  weight: number;
}

export function TourTemplateStudio({ t }: { t: Translate }) {
  const [fields, setFields] = useState<TourFieldDefinition[]>([
    { id: 'header-description', label: t('tourDescriptionLabel'), type: 'longtext', weight: 1 },
    { id: 'entry-item', label: t('tourPackingItemLabel'), type: 'longtext', weight: 4 },
    { id: 'entry-done', label: t('tourDoneLabel'), type: 'checkbox', weight: 1 },
  ]);
  const [selectedCell, setSelectedCell] = useState<CellId | null>(null);
  const [draftLabel, setDraftLabel] = useState('');
  const [draftType, setDraftType] = useState<TourFieldType>('text');

  const openField = (cellId: CellId) => {
    const field = fields.find((candidate) => candidate.id === cellId);
    if (!field) {
      return;
    }

    setSelectedCell(cellId);
    setDraftLabel(field.label);
    setDraftType(field.type);
  };

  const applyField = () => {
    if (!selectedCell || !draftLabel.trim()) {
      return;
    }

    setFields((current) => current.map((field) => field.id === selectedCell
      ? { ...field, label: draftLabel.trim(), type: draftType }
      : field));
    setSelectedCell(null);
  };

  return (
    <section className="tour-template-studio">
      <header>
        <div>
          <p className="detail-kicker">{t('tourTodoTemplate')}</p>
          <h2>{t('tourTemplateStudioTitle')}</h2>
          <p>{t('tourTemplateStudioIntro')}</p>
        </div>
        <button aria-label={t('tourTemplateHelp')} className="tour-help-tip" title={t('tourTemplateHelp')} type="button"><Icon name="help" /></button>
      </header>

      <TemplateCanvas
        cells={fields.filter((field) => field.id === 'header-description')}
        kicker={t('tourTemplateHeader')}
        onSelect={openField}
        selectedCell={selectedCell}
        t={t}
      />
      <TemplateCanvas
        cells={fields.filter((field) => field.id !== 'header-description')}
        kicker={t('tourTemplateEntry')}
        onSelect={openField}
        selectedCell={selectedCell}
        t={t}
      />

      {selectedCell ? (
        <ModalFrame className="tour-field-dialog-backdrop" onClose={() => setSelectedCell(null)}>
          <section aria-labelledby="tour-field-dialog-title" aria-modal="true" className="tour-field-dialog" role="dialog">
            <header>
              <div>
                <p className="detail-kicker">{t('tourTemplateCell')}</p>
                <h3 id="tour-field-dialog-title">{draftLabel}</h3>
              </div>
              <button aria-label={t('cancel')} className="tiny-icon-button" onClick={() => setSelectedCell(null)} type="button"><Icon name="close" /></button>
            </header>
            <label>{t('tourFieldName')}<input autoFocus onChange={(event) => setDraftLabel(event.target.value)} value={draftLabel} /></label>
            <label>{t('tourFieldType')}<select onChange={(event) => setDraftType(event.target.value as TourFieldType)} value={draftType}><option value="longtext">{t('tourTemplateLongText')}</option><option value="text">{t('tourTemplateText')}</option><option value="checkbox">{t('tourTemplateCheckbox')}</option></select></label>
            <footer>
              <button className="secondary-action" onClick={() => setSelectedCell(null)} type="button">{t('cancel')}</button>
              <button className="primary-action" disabled={!draftLabel.trim()} onClick={applyField} type="button"><Icon name="check" />{t('tourApplyField')}</button>
            </footer>
          </section>
        </ModalFrame>
      ) : null}
    </section>
  );
}

function TemplateCanvas({
  cells,
  kicker,
  onSelect,
  selectedCell,
  t,
}: {
  cells: TourFieldDefinition[];
  kicker: string;
  onSelect: (cell: CellId) => void;
  selectedCell: CellId | null;
  t: Translate;
}) {
  return (
    <section className="tour-template-canvas">
      <header><strong>{kicker}</strong><span>{t('tourTemplateClickCell')}</span></header>
      <div className="tour-template-row" style={{ gridTemplateColumns: cells.map((cell) => `${cell.weight}fr`).join(' ') }}>
        {cells.map((cell, index) => (
          <div className="tour-template-cell-wrap" key={cell.id}>
            {index > 0 ? <span className="tour-template-divider" title={t('tourTemplateDividerHint')} /> : null}
            <button aria-pressed={selectedCell === cell.id} onClick={() => onSelect(cell.id)} type="button">
              <span><strong>{cell.label}</strong><small>{fieldTypeLabel(cell.type, t)}</small></span>
              {cell.type === 'checkbox' ? <span className="tour-template-checkbox"><Icon name="check" /></span> : <span className="tour-template-lines" />}
            </button>
          </div>
        ))}
      </div>
    </section>
  );
}

function fieldTypeLabel(type: TourFieldType, t: Translate) {
  if (type === 'checkbox') {
    return t('tourTemplateCheckbox');
  }

  return type === 'longtext' ? t('tourTemplateLongText') : t('tourTemplateText');
}
