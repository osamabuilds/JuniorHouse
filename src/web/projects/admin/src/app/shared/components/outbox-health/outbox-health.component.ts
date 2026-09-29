import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { OutboxSchemaHealth } from '@core/health';

/** Events waiting longer than this (seconds) are called "late": the dispatcher normally clears them within seconds. */
export const OUTBOX_LATE_AFTER_SECONDS = 120;

/**
 * SCRUM-186: are background events flowing? The API records every business change and the events that follow it (a delivery
 * note moving the purchase order to Delivered, for instance) in an outbox, and a background worker delivers them. This shows,
 * in words, whether anything is waiting, retrying or has failed. Holds no state; the shell polls and passes the numbers in.
 */
@Component({
  selector: 'app-outbox-health',
  template: `
    @if (health(); as items) {
      <p class="outbox" [class.outbox--problem]="problem()" role="status">
        <span class="outbox__label">Background updates:</span>
        {{ summary() }}
      </p>
    }
  `,
  styles: `
    .outbox {
      margin: 0;
      font-size: var(--text-xs);
      color: var(--color-ink-soft);
    }
    .outbox__label {
      font-weight: 600;
    }
    .outbox--problem {
      color: var(--color-danger);
      font-weight: 600;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OutboxHealthComponent {
  /** Null until the first answer arrives, or when the API cannot say: nothing is shown then. */
  readonly health = input.required<readonly OutboxSchemaHealth[] | null>();

  private readonly totals = computed(() => {
    const items = this.health() ?? [];
    return {
      pending: items.reduce((sum, item) => sum + item.pending + item.inFlight, 0),
      retrying: items.reduce((sum, item) => sum + item.retrying, 0),
      failed: items.reduce((sum, item) => sum + item.deadLettered, 0),
      oldest: items.reduce((max, item) => Math.max(max, item.oldestPendingSeconds ?? 0), 0),
    };
  });

  readonly problem = computed(() => {
    const totals = this.totals();
    return totals.failed > 0 || totals.oldest > OUTBOX_LATE_AFTER_SECONDS;
  });

  readonly summary = computed(() => {
    const { pending, retrying, failed, oldest } = this.totals();
    if (failed > 0) {
      return `${failed} could not be delivered. Some status changes may not have reached other parts of the system. Ask a developer to look at the failed events.`;
    }
    if (oldest > OUTBOX_LATE_AFTER_SECONDS) {
      return `${pending} waiting for more than ${Math.round(oldest / 60)} minutes. Status changes may be late.`;
    }
    if (pending + retrying > 0) {
      return `${pending} on their way${retrying > 0 ? `, ${retrying} being retried` : ''}.`;
    }
    return 'all delivered.';
  });
}
