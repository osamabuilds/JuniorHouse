import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PUTAWAY_STATUS_TEXT } from '../../putaway.constants';
import { PutawayItemRowDto } from '../../models';

/**
 * The items still to be shelved, oldest first ("put away n of N", PUT-14). "Choose" is the way in when a variant has no printed tag yet
 * and so there is nothing to scan (PUT-19). An item waiting for its barcode, or whose barcode was retired, cannot be chosen, and the
 * row says why. Presentational only.
 */
@Component({
  selector: 'app-putaway-queue',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (items().length === 0) {
      <p class="state-message">Nothing is waiting to be put away.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Items waiting to be put away</caption>
        <thead>
          <tr>
            <th scope="col">SKU</th>
            <th scope="col">Grade</th>
            <th scope="col">Put away</th>
            <th scope="col">Status</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (item of items(); track item.id) {
            <tr>
              <td data-label="SKU">
                @if (item.skuCode) {
                  <strong>{{ item.skuCode }}</strong>
                } @else {
                  <span class="badge badge--muted">No barcode yet</span>
                }
                <br /><small>PO {{ item.poId }}</small>
              </td>
              <td data-label="Grade">{{ item.gradeCode === 'CONCESSION' ? 'Concession' : 'Accepted' }}</td>
              <td data-label="Put away">{{ item.putQuantity }} of {{ item.qcQuantity }} <small>({{ item.pending }} to go)</small></td>
              <td data-label="Status">
                {{ statusText(item) }}
              </td>
              <td data-label="Actions">
                <button type="button" class="button button--small" [disabled]="!canChoose(item)" (click)="chosen.emit(item)">Choose</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PutawayQueueComponent {
  readonly items = input.required<readonly PutawayItemRowDto[]>();
  readonly loading = input.required<boolean>();

  readonly chosen = output<PutawayItemRowDto>();

  canChoose(item: PutawayItemRowDto): boolean {
    return item.statusCode === 'PENDING' && !item.gtinRetired && item.pending > 0;
  }

  statusText(item: PutawayItemRowDto): string {
    return item.gtinRetired ? 'Barcode retired: cannot be put away' : (PUTAWAY_STATUS_TEXT[item.statusCode] ?? item.statusCode);
  }
}
