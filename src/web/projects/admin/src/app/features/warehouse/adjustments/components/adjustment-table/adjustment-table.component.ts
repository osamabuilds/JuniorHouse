import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AdjustmentDto } from '../../models';

/**
 * The adjustment history, newest first (ADJ-10): what changed, where, why, the balance before and after, who and when. There are no
 * edit or delete buttons on purpose: an adjustment is a record, and a mistake is fixed with a new, opposite one (ADJ-6). Presentational only.
 */
@Component({
  selector: 'app-adjustment-table',
  imports: [DatePipe],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (adjustments().length === 0) {
      <p class="state-message">No adjustments found.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Stock adjustments</caption>
        <thead>
          <tr>
            <th scope="col">When</th>
            <th scope="col">SKU</th>
            <th scope="col">Bin</th>
            <th scope="col">Change</th>
            <th scope="col">Before → after</th>
            <th scope="col">Why</th>
            <th scope="col">Who</th>
          </tr>
        </thead>
        <tbody>
          @for (a of adjustments(); track a.id) {
            <tr>
              <td data-label="When">{{ a.at | date: 'd MMM y, HH:mm' }}</td>
              <td data-label="SKU"><strong>{{ a.skuCode }}</strong><br /><small>{{ a.gradeCode === 'CONCESSION' ? 'concession' : 'accepted' }}</small></td>
              <td data-label="Bin">{{ a.binCode }}</td>
              <td data-label="Change"><strong>{{ a.quantityChange > 0 ? '+' : '' }}{{ a.quantityChange }}</strong></td>
              <td data-label="Before → after">{{ a.balanceBefore }} → {{ a.balanceAfter }}</td>
              <td data-label="Why">
                {{ reasonText(a.reasonCode) }}
                @if (a.note) {
                  <br /><small>{{ a.note }}</small>
                }
                @if (a.reconfirmed) {
                  <br /><small>Large change, confirmed</small>
                }
              </td>
              <td data-label="Who">{{ a.actorName }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdjustmentTableComponent {
  readonly adjustments = input.required<readonly AdjustmentDto[]>();
  readonly loading = input.required<boolean>();

  reasonText(code: string): string {
    return code === 'FOUND_STOCK' ? 'Found stock' : code.charAt(0) + code.slice(1).toLowerCase();
  }
}
