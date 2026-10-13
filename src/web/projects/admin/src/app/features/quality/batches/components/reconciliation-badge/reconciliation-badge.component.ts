import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * How a goods receipt compares with what the vendor claimed, as words (spec AC-7, AC-44: text as well as colour):
 * "Matched the delivery note", "Short by 12" or "Over by 5". Holds no state.
 */
@Component({
  selector: 'app-reconciliation-badge',
  template: `<span class="qc-flag" [class.qc-flag--ok]="code() === 'Matched'" [class.qc-flag--warn]="code() !== 'Matched'">{{ text() }}</span>`,
  styles: ':host { display: inline-block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReconciliationBadgeComponent {
  readonly code = input.required<'Matched' | 'Short' | 'Over'>();
  /** Counted minus claimed, in pieces. */
  readonly diff = input(0);

  readonly text = computed(() => {
    switch (this.code()) {
      case 'Matched':
        return 'Matched the delivery note';
      case 'Short':
        return `Short by ${Math.abs(this.diff())}`;
      default:
        return `Over by ${Math.abs(this.diff())}`;
    }
  });
}
