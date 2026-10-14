import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { BinAgeDto } from '../../models';

/**
 * Choose the bins to count (CC-1, CC-11, decision D9): never-counted bins come first and the days since each was last counted help pick
 * the next ones. A bin already in an open count, or an inactive one, cannot be ticked (CC-3). Presentational only.
 */
@Component({
  selector: 'app-bin-picker',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (bins().length === 0) {
      <p class="state-message">There are no bins yet. Add some first.</p>
    } @else {
      <fieldset>
        <legend>Choose the bins to count</legend>
        <ul class="plain-list">
          @for (bin of bins(); track bin.binId) {
            <li>
              <label>
                <input
                  type="checkbox"
                  [checked]="selected().includes(bin.binId)"
                  [disabled]="!canPick(bin)"
                  (change)="toggled.emit(bin.binId)"
                />
                <strong>{{ bin.binCode }}</strong>
                <small> {{ age(bin) }}</small>
              </label>
            </li>
          }
        </ul>
      </fieldset>
    }
  `,
  styles: ':host { display: block; } .plain-list { list-style: none; margin: 0; padding: 0; } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BinPickerComponent {
  readonly bins = input.required<readonly BinAgeDto[]>();
  readonly loading = input.required<boolean>();
  readonly selected = input.required<readonly number[]>();

  readonly toggled = output<number>();

  canPick(bin: BinAgeDto): boolean {
    return bin.isActive && !bin.inOpenCount;
  }

  age(bin: BinAgeDto): string {
    if (bin.inOpenCount) {
      return '(in an open count)';
    }

    if (!bin.isActive) {
      return '(inactive)';
    }

    if (bin.daysSinceCounted === null) {
      return '(never counted)';
    }

    return bin.daysSinceCounted === 0 ? '(counted today)' : `(counted ${bin.daysSinceCounted} day${bin.daysSinceCounted === 1 ? '' : 's'} ago)`;
  }
}
