import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { QueueItem, ageText } from '../../models';

/**
 * The pick queue, oldest first, with how long each order has waited (APP-8, PCK-1). One row per order: the order number only, never a customer.
 * Presentational: it only reports a click. Every control is at least 44 px tall (APP-5).
 */
@Component({
  selector: 'app-queue-table',
  templateUrl: './queue-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueueTableComponent {
  readonly rows = input.required<readonly QueueItem[]>();
  /** The action button's words, for example "Pick" or "Pack". */
  readonly actionLabel = input('Pick');
  /** The signed-in person's name, to tell their own orders from someone else's. */
  readonly me = input<string | null>(null);

  readonly open = output<number>();

  age(minutes: number): string {
    return ageText(minutes);
  }

  heldByOther(row: QueueItem): boolean {
    return row.claimedBy !== null && row.claimedBy !== this.me();
  }
}
