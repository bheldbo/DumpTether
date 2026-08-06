import { useState } from 'react';
import { Icon } from '../../components/Icon';
import type { Translate } from '../../localization';

type CellId = 'header-description' | 'entry-item' | 'entry-done';

export function TourTemplateStudio({ t }: { t: Translate }) {
  const [selectedCell, setSelectedCell] = useState<CellId | null>(null);

  return (
    <section className="tour-template-studio">
      <header>
        <div>
          <p className="detail-kicker">{t('tourTodoTemplate')}</p>
          <h2>{t('tourTemplateStudioTitle')}</h2>
          <p>{t('tourTemplateStudioIntro')}</p>
        </div>
        <span className="tour-help-tip" title={t('tourTemplateHelp')}><Icon name="help" /></span>
      </header>

      <TemplateCanvas
        cells={[{ id: 'header-description', label: t('tourDescriptionLabel'), type: t('tourTemplateLongText'), weight: 1 }]}
        kicker={t('tourTemplateHeader')}
        onSelect={setSelectedCell}
        selectedCell={selectedCell}
        t={t}
      />
      <TemplateCanvas
        cells={[
          { id: 'entry-item', label: t('tourPackingItemLabel'), type: t('tourTemplateLongText'), weight: 4 },
          { id: 'entry-done', label: t('tourDoneLabel'), type: t('tourTemplateCheckbox'), weight: 1 },
        ]}
        kicker={t('tourTemplateEntry')}
        onSelect={setSelectedCell}
        selectedCell={selectedCell}
        t={t}
      />

      {selectedCell ? (
        <div aria-modal="true" className="tour-field-dialog" role="dialog">
          <div>
            <p className="detail-kicker">{t('tourTemplateCell')}</p>
            <h3>{selectedCell === 'entry-done' ? t('tourDoneLabel') : selectedCell === 'entry-item' ? t('tourPackingItemLabel') : t('tourDescriptionLabel')}</h3>
          </div>
          <label>{t('tourFieldName')}<input defaultValue={selectedCell === 'entry-done' ? t('tourDoneLabel') : selectedCell === 'entry-item' ? t('tourPackingItemLabel') : t('tourDescriptionLabel')} /></label>
          <label>{t('tourFieldType')}<select defaultValue={selectedCell === 'entry-done' ? 'checkbox' : 'longtext'}><option value="longtext">{t('tourTemplateLongText')}</option><option value="text">{t('tourTemplateText')}</option><option value="checkbox">{t('tourTemplateCheckbox')}</option></select></label>
          <button className="primary-action" onClick={() => setSelectedCell(null)} type="button"><Icon name="check" />{t('done')}</button>
        </div>
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
  cells: Array<{ id: CellId; label: string; type: string; weight: number }>;
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
              <span><strong>{cell.label}</strong><small>{cell.type}</small></span>
              {cell.type === t('tourTemplateCheckbox') ? <span className="tour-template-checkbox"><Icon name="check" /></span> : <span className="tour-template-lines" />}
            </button>
          </div>
        ))}
      </div>
    </section>
  );
}
