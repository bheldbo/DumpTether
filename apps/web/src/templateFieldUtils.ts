import type { EditableTemplateField } from './appTypes';
import type {
  FieldDefinitionScope,
  FieldValueMap,
  FieldValuePrimitive,
  TaskTemplateDetailResponse,
} from './types';

export function withDefaultFieldValues(
  fields: TaskTemplateDetailResponse['fields'],
  values: FieldValueMap,
): FieldValueMap {
  return Object.fromEntries(
    fields.map((field) => [
      field.id,
      values[field.id] ?? (field.type === 'Checkbox' ? false : null),
    ]),
  );
}

export function entryFieldsHaveContent(
  fields: TaskTemplateDetailResponse['fields'],
  values: FieldValueMap,
): boolean {
  return fields.some((field) => {
    const value = values[field.id];

    if (typeof value === 'string') {
      return value.trim().length > 0;
    }

    return value === true;
  });
}

export function fieldValueIsEmpty(value: FieldValuePrimitive): boolean {
  return value === null || value === '' ||
    (typeof value === 'string' && value.trim().length === 0);
}

export function toEditableTemplateField(
  field: TaskTemplateDetailResponse['fields'][number],
): EditableTemplateField {
  return {
    clientId: field.id,
    id: field.id,
    name: field.name,
    type: field.type,
    scope: field.scope,
    required: field.required,
    sortOrder: field.sortOrder,
    optionsText: field.options.join('\n'),
    layoutRow: field.layoutRow ?? 1,
    layoutColumn: field.layoutColumn ?? 1,
    layoutRowSpan: field.layoutRowSpan ?? 1,
    layoutColumnSpan: 1,
    layoutWeight: field.layoutWeight ?? 1,
  };
}

export function renumberTemplateFields(
  fields: EditableTemplateField[],
): EditableTemplateField[] {
  const sortOrders: Record<FieldDefinitionScope, number> = {
    Header: 0,
    Entry: 0,
  };

  return fields.map((field) => {
    const sortOrder = sortOrders[field.scope];
    sortOrders[field.scope] += 1;

    return {
      ...field,
      sortOrder,
    };
  });
}

export function splitOptions(optionsText: string) {
  return optionsText
    .split(/\r?\n/)
    .map((option) => option.trim())
    .filter(Boolean);
}

export function clampInteger(value: number, min: number, max: number) {
  if (!Number.isFinite(value)) {
    return min;
  }

  return Math.min(max, Math.max(min, Math.round(value)));
}
