import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PickRow } from '../../models';

/**
 * The pick list in walking order (PCK-1): shelf, item, size, colour and how many of how many are picked. A row waiting for restock has no shelf
 * and says so in words. Presentational.
 */
@Component({
  selector: 'app-pick-rows',
  templateUrl: './pick-rows.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PickRowsComponent {
  readonly rows = input.required<readonly PickRow[]>();
  /** The shelf the picker is standing at, highlighted. */
  readonly currentBin = input<string | null>(null);

  isWaiting(row: PickRow): boolean {
    return row.status === 'AWAITING_RESTOCK';
  }

  isDone(row: PickRow): boolean {
    return row.picked >= row.quantity && row.quantity > 0;
  }
}
