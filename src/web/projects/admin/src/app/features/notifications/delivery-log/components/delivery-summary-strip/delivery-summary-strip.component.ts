import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DeliverySummary } from '../../models';
import { waitText } from '../../utils/delivery-labels.util';

/**
 * The strip above the delivery log (docs/specs/notifications/delivery-log, DLV-11, CHN-15): how long the oldest due message has waited, what
 * needs a person (dead letters not yet resent, failures), and how many WhatsApp messages went out today, which is the cost driver.
 */
@Component({
  selector: 'app-delivery-summary-strip',
  templateUrl: './delivery-summary-strip.component.html',
  styleUrl: './delivery-summary-strip.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliverySummaryStripComponent {
  readonly summary = input.required<DeliverySummary>();

  readonly wait = computed(() => waitText(this.summary().oldestDueSeconds));
  readonly failed = computed(() => this.summary().countsByStatus['FAIL'] ?? 0);
  readonly late = computed(() => this.summary().oldestDueSeconds >= 15 * 60);
}
