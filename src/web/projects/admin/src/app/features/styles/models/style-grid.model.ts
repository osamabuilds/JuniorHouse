import { LookupDto } from '@features/reference-data';

/** One editable cell of the colour x size target-quantity grid (AC-3). */
export interface GridCell {
  readonly sizeId: number;
  readonly colourId: number;
  qty: number;
}

export interface GridRow {
  readonly size: LookupDto;
  readonly cells: GridCell[];
}

export interface GridQtyChange {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
}

export function gridKey(sizeId: number, colourId: number): string {
  return `${sizeId}-${colourId}`;
}
