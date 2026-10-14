import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AdjustmentReasonDto } from '@features/warehouse/adjustments';
import { LARGE_VARIANCE_HINT } from '../../cycle-counts.constants';
import { CountReviewDto, CountReviewLineDto, ReasonChoice, reviewKey } from '../../models';

/**
 * The review step (CC-4, CC-7, CC-8): counted against system for every line, the difference, and a reason for each difference.
 * This is the only place a system quantity appears, and only after counts are in. A line flagged as changed since the count opened
 * must be recounted before the count can be confirmed. A large difference asks to be confirmed. Presentational only.
 */
@Component({
  selector: 'app-review-table',
  template: `
    <section class="card" aria-labelledby="rt-heading">
      <h2 id="rt-heading">Review {{ review().countNo }}</h2>

      @if (!review().allBinsSubmitted) {
        <p class="banner banner--error" role="alert">Not every bin has been counted yet. Count them all before confirming.</p>
      }
      @if (hasStale()) {
        <p class="banner banner--error" role="alert">
          Stock moved in some bins after the count opened. Count those bins again, ticking "recount", before you confirm.
        </p>
      }

      @if (review().lines.length === 0) {
        <p class="state-message">Nothing was counted and the counted bins hold nothing.</p>
      } @else {
        <table class="responsive">
          <caption class="sr-only">Counted against the system</caption>
          <thead>
            <tr>
              <th scope="col">Bin</th>
              <th scope="col">SKU</th>
              <th scope="col">Counted</th>
              <th scope="col">System</th>
              <th scope="col">Difference</th>
              <th scope="col">Reason</th>
            </tr>
          </thead>
          <tbody>
            @for (line of review().lines; track key(line)) {
              <tr>
                <td data-label="Bin">{{ line.binCode }}</td>
                <td data-label="SKU">
                  {{ line.skuCode }} <small>({{ line.gradeCode === 'CONCESSION' ? 'concession' : 'accepted' }})</small>
                  @if (line.notCounted) {
                    <br /><small>Not scanned: counted as none</small>
                  }
                  @if (line.stale) {
                    <br /><span class="badge badge--danger">Changed since the count opened</span>
                  }
                </td>
                <td data-label="Counted">{{ line.countedQuantity }}</td>
                <td data-label="System">{{ line.systemQuantity }}</td>
                <td data-label="Difference"><strong>{{ line.variance > 0 ? '+' : '' }}{{ line.variance }}</strong></td>
                <td data-label="Reason">
                  @if (line.variance === 0) {
                    <span class="badge badge--muted">No difference</span>
                  } @else {
                    <label>
                      <span class="sr-only">Reason for the difference on {{ line.skuCode }} in {{ line.binCode }}</span>
                      <select (change)="chooseReason(line, $event)">
                        <option value="0" [selected]="reasonOf(line) === 0">Choose a reason…</option>
                        @for (reason of reasons(); track reason.id) {
                          <option [value]="reason.id" [selected]="reasonOf(line) === reason.id">{{ reason.name }}</option>
                        }
                      </select>
                    </label>
                    @if (isLarge(line)) {
                      <label class="checkbox">
                        <input type="checkbox" [checked]="choices()[key(line)]?.reconfirmed ?? false" (change)="toggleReconfirm(line, $event)" />
                        I confirm this large change
                      </label>
                    }
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      <div class="form-actions">
        <button type="button" class="button button--primary" [disabled]="!canConfirm() || confirming()" (click)="confirmed.emit()">
          {{ confirming() ? 'Confirming…' : 'Confirm the count' }}
        </button>
        <button type="button" class="button" (click)="closed.emit()">Back to counting</button>
      </div>
    </section>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewTableComponent {
  readonly review = input.required<CountReviewDto>();
  readonly reasons = input.required<readonly AdjustmentReasonDto[]>();
  readonly choices = input.required<Readonly<Record<string, ReasonChoice>>>();
  readonly canConfirm = input(false);
  readonly confirming = input(false);

  readonly reasonChosen = output<{ key: string; reasonId: number }>();
  readonly reconfirmToggled = output<{ key: string; reconfirmed: boolean }>();
  readonly confirmed = output<void>();
  readonly closed = output<void>();

  key(line: CountReviewLineDto): string {
    return reviewKey(line);
  }

  hasStale(): boolean {
    return this.review().lines.some((l) => l.stale);
  }

  reasonOf(line: CountReviewLineDto): number {
    return this.choices()[reviewKey(line)]?.reasonId ?? 0;
  }

  isLarge(line: CountReviewLineDto): boolean {
    return Math.abs(line.variance) > LARGE_VARIANCE_HINT;
  }

  chooseReason(line: CountReviewLineDto, event: Event): void {
    this.reasonChosen.emit({ key: reviewKey(line), reasonId: Number((event.target as HTMLSelectElement).value) });
  }

  toggleReconfirm(line: CountReviewLineDto, event: Event): void {
    this.reconfirmToggled.emit({ key: reviewKey(line), reconfirmed: (event.target as HTMLInputElement).checked });
  }
}
