import type { CSSProperties } from 'react';

export const FIELD_LAYOUT_MAX_COLUMNS = 6;
const FIELD_LAYOUT_MAX_ROWS = 24;

export interface FieldLayoutShape {
  id?: string;
  clientId?: string;
  name?: string;
  sortOrder?: number;
  type?: string;
  layoutRow?: number | null;
  layoutColumn?: number | null;
  layoutRowSpan?: number | null;
  layoutColumnSpan?: number | null;
}

export type NormalizedFieldLayout<T extends FieldLayoutShape> = T & {
  layoutRow: number;
  layoutColumn: number;
  layoutRowSpan: number;
  layoutColumnSpan: number;
  layoutWasAdjusted: boolean;
};

export function normalizeTemplateLayoutFields<T extends FieldLayoutShape>(
  fields: T[],
): NormalizedFieldLayout<T>[] {
  const occupiedCells = new Set<string>();
  const normalizedByIndex = new Map<number, NormalizedFieldLayout<T>>();

  fields
    .map((field, index) => ({ field, index }))
    .sort((first, second) => {
      const firstRow = cleanInteger(first.field.layoutRow, 1, FIELD_LAYOUT_MAX_ROWS);
      const secondRow = cleanInteger(second.field.layoutRow, 1, FIELD_LAYOUT_MAX_ROWS);

      if (firstRow !== secondRow) {
        return firstRow - secondRow;
      }

      const firstColumn = cleanInteger(first.field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS);
      const secondColumn = cleanInteger(second.field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS);

      if (firstColumn !== secondColumn) {
        return firstColumn - secondColumn;
      }

      return (first.field.sortOrder ?? first.index) - (second.field.sortOrder ?? second.index);
    })
    .forEach(({ field, index }) => {
      const requestedRow = cleanInteger(field.layoutRow, 1, FIELD_LAYOUT_MAX_ROWS);
      const requestedRowSpan = cleanInteger(field.layoutRowSpan, 1, 8);
      const requestedColumnSpan = cleanInteger(
        field.layoutColumnSpan,
        defaultColumnSpan(field),
        FIELD_LAYOUT_MAX_COLUMNS,
      );
      const columnSpan = Math.min(requestedColumnSpan, FIELD_LAYOUT_MAX_COLUMNS);
      const requestedColumn = Math.min(
        cleanInteger(field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS),
        FIELD_LAYOUT_MAX_COLUMNS - columnSpan + 1,
      );
      let row = requestedRow;
      let column = requestedColumn;

      while (
        layoutCellsAreOccupied(
          occupiedCells,
          row,
          column,
          requestedRowSpan,
          columnSpan,
        )
      ) {
        column += 1;

        if (column + columnSpan - 1 > FIELD_LAYOUT_MAX_COLUMNS) {
          column = 1;
          row += 1;
        }
      }

      occupyLayoutCells(occupiedCells, row, column, requestedRowSpan, columnSpan);

      normalizedByIndex.set(index, {
        ...field,
        layoutRow: row,
        layoutColumn: column,
        layoutRowSpan: requestedRowSpan,
        layoutColumnSpan: columnSpan,
        layoutWasAdjusted:
          row !== requestedRow ||
          column !== requestedColumn ||
          requestedRowSpan !== (field.layoutRowSpan ?? 1) ||
          columnSpan !== (field.layoutColumnSpan ?? defaultColumnSpan(field)),
      });
    });

  return fields.map((field, index) => normalizedByIndex.get(index) ?? {
    ...field,
    layoutRow: 1,
    layoutColumn: 1,
    layoutRowSpan: 1,
    layoutColumnSpan: defaultColumnSpan(field),
    layoutWasAdjusted: false,
  });
}

export function getTemplateLayoutGridStyle(
  fields: FieldLayoutShape[],
): CSSProperties {
  const normalizedFields = normalizeTemplateLayoutFields(fields);
  const columnCount = Math.max(
    1,
    ...normalizedFields.map((field) => field.layoutColumn + field.layoutColumnSpan - 1),
  );

  return {
    '--template-layout-columns': columnCount,
    gridTemplateColumns: `repeat(${columnCount}, minmax(0, 1fr))`,
  } as CSSProperties;
}

export function getEditableTemplateFieldGridStyle(
  field: FieldLayoutShape,
): CSSProperties {
  const layoutRow = cleanInteger(field.layoutRow, 1, FIELD_LAYOUT_MAX_ROWS);
  const layoutRowSpan = cleanInteger(field.layoutRowSpan, 1, 8);
  const layoutColumnSpan = cleanInteger(
    field.layoutColumnSpan,
    defaultColumnSpan(field),
    FIELD_LAYOUT_MAX_COLUMNS,
  );
  const layoutColumn = Math.min(
    cleanInteger(field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS),
    FIELD_LAYOUT_MAX_COLUMNS - layoutColumnSpan + 1,
  );

  return {
    gridColumn: `${layoutColumn} / span ${layoutColumnSpan}`,
    gridRow: `${layoutRow} / span ${layoutRowSpan}`,
  };
}

function layoutCellsAreOccupied(
  occupiedCells: Set<string>,
  row: number,
  column: number,
  rowSpan: number,
  columnSpan: number,
) {
  for (let rowOffset = 0; rowOffset < rowSpan; rowOffset += 1) {
    for (let columnOffset = 0; columnOffset < columnSpan; columnOffset += 1) {
      if (occupiedCells.has(`${row + rowOffset}:${column + columnOffset}`)) {
        return true;
      }
    }
  }

  return false;
}

function occupyLayoutCells(
  occupiedCells: Set<string>,
  row: number,
  column: number,
  rowSpan: number,
  columnSpan: number,
) {
  for (let rowOffset = 0; rowOffset < rowSpan; rowOffset += 1) {
    for (let columnOffset = 0; columnOffset < columnSpan; columnOffset += 1) {
      occupiedCells.add(`${row + rowOffset}:${column + columnOffset}`);
    }
  }
}

function defaultColumnSpan(field: FieldLayoutShape) {
  return field.type === 'LongText' ? 2 : 1;
}

function cleanInteger(value: number | null | undefined, fallback: number, max: number) {
  if (!Number.isFinite(value)) {
    return fallback;
  }

  return Math.min(max, Math.max(1, Math.round(value!)));
}
