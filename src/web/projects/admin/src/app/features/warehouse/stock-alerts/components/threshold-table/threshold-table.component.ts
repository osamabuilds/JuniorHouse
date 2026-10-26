import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MAX_THRESHOLD } from '../../stock-alerts.constants';
import { ThresholdRowDto } from '../../models';
import { HasPermissionDirective } from '@core/auth';

export interface ThresholdChange {
  readonly variantRefId: number;
  readonly quantity: number;
}

/**
 * The low-stock threshold of each variant that has held stock (ALR-6): its own override or the default. Each row has a labelled number
 * box and a Save button; a threshold of 0 means "alert only at stock-out". The box is checked here (a whole number from 0 up) and again
 * by the server. Presentational only.
 */
@Component({
  imports: [HasPermissionDirective],
  selector: 'app-threshold-table',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (rows().length === 0) {
      <p class="state-message">No variants have held stock yet.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Low-stock thresholds by variant</caption>
        <thead>
          <tr>
            <th scope="col">SKU</th>
            <th scope="col">Available</th>
            <th scope="col">Alert at or below</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track row.variantRefId) {
            <tr>
              <td data-label="SKU">
                <strong>{{ row.skuCode }}</strong>
                @if (!row.isOverride) {
                  <br /><small>Using the default</small>
                }
              </td>
              <td data-label="Available">{{ row.available }}</td>
              <td data-label="Alert at or below">
                <label>
                  <span class="sr-only">Low-stock threshold for {{ row.skuCode }}</span>
                  <input
                    #box
                    type="number"
                    inputmode="numeric"
                    min="0"
                    [max]="max"
                    step="1"
                    [value]="row.effectiveThreshold"
                    [attr.aria-invalid]="invalid(box.value) ? 'true' : null"
                    (input)="edited()"
                  />
                </label>
                @if (invalid(box.value)) {
                  <small role="alert">Enter a whole number from 0 to {{ max }}.</small>
                }
              </td>
              <td data-label="Actions">
                <button *hasPermission="'inventory.write'"
                  type="button"
                  class="button button--small"
                  [disabled]="savingVariantId() === row.variantRefId || invalid(box.value)"
                  (click)="save.emit({ variantRefId: row.variantRefId, quantity: +box.value })"
                >
                  {{ savingVariantId() === row.variantRefId ? 'Saving…' : 'Save' }}
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; } small { color: var(--color-muted, #555); } input { width: 7rem; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThresholdTableComponent {
  readonly rows = input.required<readonly ThresholdRowDto[]>();
  readonly loading = input.required<boolean>();
  readonly savingVariantId = input<number | null>(null);

  readonly save = output<ThresholdChange>();

  readonly max = MAX_THRESHOLD;

  /** Bumped on every keystroke so the OnPush view re-checks each box as it is typed in. */
  private readonly keystrokes = signal(0);

  edited(): void {
    this.keystrokes.update((n) => n + 1);
  }

  invalid(raw: string): boolean {
    this.keystrokes();
    const value = Number(raw);
    return raw.trim() === '' || !Number.isInteger(value) || value < 0 || value > MAX_THRESHOLD;
  }
}
