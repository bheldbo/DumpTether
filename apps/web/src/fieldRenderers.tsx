import type { FieldValueResponse } from './types';

interface FieldValueListProps {
  fieldValues: FieldValueResponse[];
}

export function FieldValueList({ fieldValues }: FieldValueListProps) {
  if (fieldValues.length === 0) {
    return (
      <p className="empty-copy">
        No structured fields yet. Template fields will appear here when they are
        added.
      </p>
    );
  }

  return (
    <dl className="field-list">
      {fieldValues.map((fieldValue) => (
        <div className="field-row" key={fieldValue.id}>
          <dt>{fieldValue.fieldDefinitionId}</dt>
          <dd>
            <FieldValue valueJson={fieldValue.valueJson} />
          </dd>
        </div>
      ))}
    </dl>
  );
}

function FieldValue({ valueJson }: { valueJson: string }) {
  const parsedValue = parseFieldValue(valueJson);

  if (parsedValue === null) {
    return <span className="field-empty">Empty</span>;
  }

  if (typeof parsedValue === 'boolean') {
    return <span>{parsedValue ? 'Yes' : 'No'}</span>;
  }

  if (typeof parsedValue === 'number' || typeof parsedValue === 'string') {
    return <span>{String(parsedValue)}</span>;
  }

  return <code>{JSON.stringify(parsedValue)}</code>;
}

function parseFieldValue(valueJson: string): unknown {
  try {
    return JSON.parse(valueJson);
  } catch {
    return valueJson;
  }
}
