import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ApiError } from '@core/http';
import { VendorScorecardDto } from '../../models';

/**
 * A vendor's scorecard (FR-SC-01, Sprint 4, spec AC-35..AC-37, AC-39): on-time %, on-quantity % and defect rate, all time and over
 * the rolling window, each with the counts it rests on. A measure with nothing behind it says "No data yet", never 0% or 100%, and
 * deliveries that were late only because Romp shipped the fabric late are counted out of the on-time figure and shown as a count.
 */
@Component({
  selector: 'app-scorecard-panel',
  template: `
    <section class="card" aria-labelledby="scorecard-heading">
      <h2 id="scorecard-heading">{{ vendorName() }}: scorecard</h2>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      } @else if (loading()) {
        <p class="state-message">Loading…</p>
      } @else if (scorecard(); as card) {
        <table class="responsive">
          <caption class="sr-only">
            Vendor scorecard
          </caption>
          <thead>
            <tr>
              <th scope="col">Measure</th>
              <th scope="col">All time</th>
              <th scope="col">Last {{ card.windowDays }} days</th>
              <th scope="col">Based on</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <th scope="row" data-label="Measure">On time</th>
              <td data-label="All time">{{ percent(card.onTimePct) }}</td>
              <td data-label="Last days">{{ percent(card.onTimePctRolling) }}</td>
              <td data-label="Based on">
                {{ card.deliveriesOnTime }} of {{ card.deliveriesCounted - card.deliveriesExcludedRompDelay }} deliveries
                @if (card.deliveriesExcludedRompDelay > 0) {
                  <span class="field-hint">
                    ({{ card.deliveriesExcludedRompDelay }} left out because Romp sent the fabric late)
                  </span>
                }
              </td>
            </tr>
            <tr>
              <th scope="row" data-label="Measure">On quantity</th>
              <td data-label="All time">{{ percent(card.onQuantityPct) }}</td>
              <td data-label="Last days">{{ percent(card.onQuantityPctRolling) }}</td>
              <td data-label="Based on">{{ card.deliveriesOnQuantity }} of {{ card.deliveriesCounted }} deliveries</td>
            </tr>
            <tr>
              <th scope="row" data-label="Measure">Defect rate</th>
              <td data-label="All time">{{ percent(card.defectRatePct) }}</td>
              <td data-label="Last days">{{ percent(card.defectRatePctRolling) }}</td>
              <td data-label="Based on">{{ card.defectiveUnits }} defective of {{ card.inspectedUnits }} pieces inspected (first inspection of each batch)</td>
            </tr>
          </tbody>
        </table>
      }

      <div class="form-actions">
        <button type="button" class="button" (click)="closed.emit()">Close scorecard</button>
      </div>
    </section>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScorecardPanelComponent {
  readonly vendorName = input.required<string>();
  readonly scorecard = input<VendorScorecardDto | null>(null);
  readonly loading = input(false);
  readonly error = input<ApiError | null>(null);

  readonly closed = output<void>();

  /** "No data yet" for a measure with nothing behind it; a real 0% is shown as 0%. */
  percent(value: number | null): string {
    return value === null ? 'No data yet' : `${value}%`;
  }
}
