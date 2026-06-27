import type { CSSProperties } from 'react';
import type { TaskTemplateLayoutRow as TemplateLayoutConfigRow } from './types';

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
  layoutWeight?: number | null;
}

export type NormalizedFieldLayout<T extends FieldLayoutShape> = T & {
  layoutRow: number;
  layoutColumn: number;
  layoutRowSpan: number;
  layoutColumnSpan: number;
  layoutWasAdjusted: boolean;
};

export interface TemplateLayoutRow<T extends FieldLayoutShape> {
  columnCount: number;
  fields: NormalizedFieldLayout<T>[];
  height: number;
  row: number;
  style: CSSProperties;
  weights: number[];
}

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
      const requestedColumnSpan = 1;
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
          columnSpan !== 1,
      });
    });

  return fields.map((field, index) => normalizedByIndex.get(index) ?? {
    ...field,
    layoutRow: 1,
    layoutColumn: 1,
    layoutRowSpan: 1,
    layoutColumnSpan: 1,
    layoutWasAdjusted: false,
  });
}

export function getTemplateLayoutRows<T extends FieldLayoutShape>(
  fields: T[],
  layoutRows: TemplateLayoutConfigRow[] = [],
): TemplateLayoutRow<T>[] {
  const normalizedFields = normalizeTemplateLayoutFields(fields);
  const layoutRowsByNumber = new Map(
    layoutRows
      .map((row) => ({
        ...row,
        row: cleanInteger(row.row, 1, FIELD_LAYOUT_MAX_ROWS),
      }))
      .filter((row) => row.row >= 1)
      .map((row) => [row.row, row]),
  );
  const rowCount = Math.max(
    1,
    ...normalizedFields.map((field) => field.layoutRow),
    ...layoutRowsByNumber.keys(),
  );

  return Array.from({ length: rowCount }, (_, rowIndex) => {
    const row = rowIndex + 1;
    const layoutRow = layoutRowsByNumber.get(row);
    const rowFields = normalizedFields
      .filter((field) => field.layoutRow === row)
      .sort((first, second) =>
        first.layoutColumn - second.layoutColumn ||
        (first.sortOrder ?? 0) - (second.sortOrder ?? 0));
    const columnCount = Math.max(
      1,
      layoutRow?.columnWeights.length ?? 0,
      ...rowFields.map((field) => field.layoutColumn + field.layoutColumnSpan - 1),
    );
    const weights = Array.from({ length: columnCount }, (_, columnIndex) => {
      const field = rowFields.find(
        (candidate) => candidate.layoutColumn === columnIndex + 1,
      );

      return cleanWeight(layoutRow?.columnWeights[columnIndex] ?? field?.layoutWeight);
    });
    const height = cleanRowHeight(
      layoutRow?.height,
      rowFields.some((field) => field.type === 'LongText') ? 190 : 132,
    );

    return {
      columnCount,
      fields: rowFields,
      height,
      row,
      style: {
        '--template-layout-columns': columnCount,
        '--template-layout-row-height': `${Math.round(height)}px`,
        gridTemplateColumns: weights.map((weight) => `${weight}fr`).join(' '),
        minHeight: `var(--template-layout-row-height)`,
      } as CSSProperties,
      weights,
    };
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

export function getTemplateLayoutCellStyle(
  field: FieldLayoutShape,
): CSSProperties {
  const layoutColumn = cleanInteger(field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS);

  return {
    gridColumn: layoutColumn,
  };
}

export function getEditableTemplateFieldGridStyle(
  field: FieldLayoutShape,
): CSSProperties {
  const layoutRow = cleanInteger(field.layoutRow, 1, FIELD_LAYOUT_MAX_ROWS);
  const layoutRowSpan = cleanInteger(field.layoutRowSpan, 1, 8);
  const layoutColumnSpan = 1;
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

function cleanInteger(value: number | null | undefined, fallback: number, max: number) {
  if (!Number.isFinite(value)) {
    return fallback;
  }

  return Math.min(max, Math.max(1, Math.round(value!)));
}

function cleanWeight(value: number | null | undefined) {
  if (!Number.isFinite(value)) {
    return 1;
  }

  return Math.min(12, Math.max(0.1, value!));
}

function cleanRowHeight(value: number | null | undefined, fallback: number) {
  if (!Number.isFinite(value)) {
    return fallback;
  }

  return Math.min(420, Math.max(72, value!));
}
