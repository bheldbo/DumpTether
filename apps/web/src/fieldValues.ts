import type { FieldValueMap, FieldValuePrimitive, FieldValueResponse } from './types';

export function toFieldValueMap(fieldValues: FieldValueResponse[]): FieldValueMap {
  return Object.fromEntries(
    fieldValues.map((fieldValue) => [
      fieldValue.fieldDefinitionId,
      parseFieldValue(fieldValue.valueJson),
    ]),
  );
}

function parseFieldValue(valueJson: string): FieldValuePrimitive {
  try {
    const value = JSON.parse(valueJson) as unknown;

    if (typeof value === 'string' || typeof value === 'boolean' || value === null) {
      return value;
    }

    return JSON.stringify(value);
  } catch {
    return valueJson;
  }
}
