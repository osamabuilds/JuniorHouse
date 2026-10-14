/** Which screen the purchase-orders page is showing. */
export type PoPageMode = 'list' | 'create' | 'edit' | 'detail';

/** Which inline panel is open in the detail view (amend / vendor response / send confirmation). */
export type PoPanel = 'none' | 'amend' | 'response' | 'send';

/** One size x colour combination of the PO's style. */
export interface StyleCell {
  readonly sizeId: number;
  readonly colourId: number;
}

/** One editable quantity row of the PO's line grid. */
export interface LineRow {
  readonly sizeId: number;
  readonly colourId: number;
  qty: number;
}

export interface LineQtyChange {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
}

export function lineKey(sizeId: number, colourId: number): string {
  return `${sizeId}-${colourId}`;
}
