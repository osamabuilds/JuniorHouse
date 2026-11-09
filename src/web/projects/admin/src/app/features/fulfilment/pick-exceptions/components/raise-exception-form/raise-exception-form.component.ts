import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { REASON_CHOICES, RaiseExceptionRequest } from '../../models';

/** A line the picker can flag: the variant, the bin and a readable label (for example "Floral Dress, Red, 2-3Y at A-01-1"). */
export interface FlaggableLine {
  readonly key: string;
  readonly variantId: number;
  readonly binCode: string;
  readonly label: string;
  /** How many units the system expects the picker to take from this bin still. */
  readonly remaining: number;
}

/**
 * The picker says the shelf does not hold what the system says (PEX-1, PEX-5): choose the line, say why, and enter the count found, a whole number
 * from 0 to the units still to take. Presentational: it reports the request and holds only its own form values.
 */
@Component({
  selector: 'app-raise-exception-form',
  templateUrl: './raise-exception-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RaiseExceptionFormComponent {
  readonly lines = input.required<readonly FlaggableLine[]>();
  readonly busy = input(false);

  readonly raised = output<RaiseExceptionRequest>();
  readonly cancelled = output();

  protected readonly reasons = REASON_CHOICES;
  protected readonly lineKey = signal('');
  protected readonly reason = signal('');
  protected readonly found = signal('0');
  protected readonly tried = signal(false);

  protected readonly selected = computed(() => this.lines().find((l) => l.key === (this.lineKey() || this.lines()[0]?.key)) ?? null);
  protected readonly foundNumber = computed(() => Number(this.found()));
  protected readonly foundProblem = computed(() => {
    const line = this.selected();
    const n = this.foundNumber();
    if (line === null) {
      return 'There is no line left to flag.';
    }

    return Number.isInteger(n) && n >= 0 && n <= line.remaining ? null : `Enter a whole number from 0 to ${line.remaining}.`;
  });
  protected readonly reasonProblem = computed(() => (this.reason() ? null : 'Choose a reason.'));

  protected submit(event: Event): void {
    event.preventDefault();
    this.tried.set(true);
    const line = this.selected();
    if (line === null || this.foundProblem() || this.reasonProblem()) {
      return;
    }

    this.raised.emit({ variantId: line.variantId, binCode: line.binCode, reason: this.reason(), foundCount: this.foundNumber() });
  }

  protected pick(field: 'lineKey' | 'reason' | 'found', event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLSelectElement).value;
    this[field].set(value);
  }
}
