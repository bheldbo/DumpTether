import type {
  FieldDefinitionResponse,
  FieldValueMap,
  FieldValuePrimitive,
  FieldValueResponse,
} from './types';
import { toFieldValueMap } from './fieldValues';
import {
  getTemplateLayoutCellStyle,
  getTemplateLayoutRows,
} from './templateLayout';

interface FieldValueListProps {
  fields: FieldDefinitionResponse[];
  fieldValues: FieldValueResponse[];
}

interface FieldEditorListProps {
  fields: FieldDefinitionResponse[];
  values: FieldValueMap;
  onChange: (fieldId: string, value: FieldValuePrimitive) => void;
}

export function FieldValueList({ fields, fieldValues }: FieldValueListProps) {
  const valueMap = toFieldValueMap(fieldValues);
  const layoutRows = getTemplateLayoutRows(fields);

  if (fields.length === 0) {
    return (
      <p className="empty-copy">
        No structured fields yet. Template fields will appear here when they are
        added.
      </p>
    );
  }

  return (
    <dl className="field-list field-value-layout">
      {layoutRows.map((row) => (
        <div className="field-value-layout-row" key={row.row} style={row.style}>
          {row.fields.map((field) => (
            <div
              className="field-row"
              data-field-type={field.type}
              key={field.id}
              style={getTemplateLayoutCellStyle(field)}
            >
              <dt>{field.name}</dt>
              <dd>
                <FieldValue field={field} value={valueMap[field.id] ?? null} />
              </dd>
            </div>
          ))}
        </div>
      ))}
    </dl>
  );
}

export function FieldEditorList({ fields, values, onChange }: FieldEditorListProps) {
  if (fields.length === 0) {
    return <p className="empty-copy">This template has no custom fields.</p>;
  }

  const layoutRows = getTemplateLayoutRows(fields);

  return (
    <div className="field-editor-list">
      {layoutRows.map((row) => (
        <div className="field-editor-layout-row" key={row.row} style={row.style}>
          {row.fields.map((field) => (
            <label
              className="field-editor"
              data-field-type={field.type}
              key={field.id}
              style={getTemplateLayoutCellStyle(field)}
            >
              <span>
                {field.name}
                {field.required ? <strong aria-label="required"> *</strong> : null}
              </span>
              <FieldEditor
                field={field}
                onChange={(value) => onChange(field.id, value)}
                value={values[field.id] ?? getEmptyValue(field)}
              />
            </label>
          ))}
        </div>
      ))}
    </div>
  );
}

export function TextField({
  onChange,
  required,
  value,
}: {
  onChange: (value: string) => void;
  required: boolean;
  value: FieldValuePrimitive;
}) {
  return (
    <input
      onChange={(event) => onChange(event.target.value)}
      required={required}
      type="text"
      value={typeof value === 'string' ? value : ''}
    />
  );
}

export function LongTextField({
  onChange,
  required,
  value,
}: {
  onChange: (value: string) => void;
  required: boolean;
  value: FieldValuePrimitive;
}) {
  return (
    <textarea
      onChange={(event) => onChange(event.target.value)}
      required={required}
      rows={4}
      value={typeof value === 'string' ? value : ''}
    />
  );
}

export function DateField({
  onChange,
  required,
  value,
}: {
  onChange: (value: string | null) => void;
  required: boolean;
  value: FieldValuePrimitive;
}) {
  return (
    <input
      onChange={(event) => onChange(event.target.value || null)}
      required={required}
      type="date"
      value={typeof value === 'string' ? value.slice(0, 10) : ''}
    />
  );
}

export function CheckboxField({
  onChange,
  value,
}: {
  onChange: (value: boolean) => void;
  value: FieldValuePrimitive;
}) {
  return (
    <input
      checked={value === true}
      onChange={(event) => onChange(event.target.checked)}
      type="checkbox"
    />
  );
}

export function SelectField({
  field,
  onChange,
  required,
  value,
}: {
  field: FieldDefinitionResponse;
  onChange: (value: string | null) => void;
  required: boolean;
  value: FieldValuePrimitive;
}) {
  return (
    <select
      onChange={(event) => onChange(event.target.value || null)}
      required={required}
      value={typeof value === 'string' ? value : ''}
    >
      <option value="">No selection</option>
      {field.options.map((option) => (
        <option key={option} value={option}>
          {option}
        </option>
      ))}
    </select>
  );
}

function FieldValue({
  field,
  value,
}: {
  field: FieldDefinitionResponse;
  value: FieldValuePrimitive;
}) {
  if (value === null || value === '') {
    return <span className="field-empty" aria-label="Empty" />;
  }

  if (field.type === 'Checkbox') {
    return <span>{value === true ? 'Yes' : 'No'}</span>;
  }

  if (field.type === 'Date' && typeof value === 'string') {
    return <span>{value}</span>;
  }

  return <span>{String(value)}</span>;
}

function FieldEditor({
  field,
  onChange,
  value,
}: {
  field: FieldDefinitionResponse;
  onChange: (value: FieldValuePrimitive) => void;
  value: FieldValuePrimitive;
}) {
  switch (field.type) {
    case 'LongText':
      return (
        <LongTextField
          onChange={onChange}
          required={field.required}
          value={value}
        />
      );
    case 'Date':
      return (
        <DateField
          onChange={onChange}
          required={field.required}
          value={value}
        />
      );
    case 'Checkbox':
      return <CheckboxField onChange={onChange} value={value} />;
    case 'Select':
      return (
        <SelectField
          field={field}
          onChange={onChange}
          required={field.required}
          value={value}
        />
      );
    case 'Text':
    default:
      return (
        <TextField
          onChange={onChange}
          required={field.required}
          value={value}
        />
      );
  }
}

function getEmptyValue(field: FieldDefinitionResponse): FieldValuePrimitive {
  return field.type === 'Checkbox' ? false : '';
}
