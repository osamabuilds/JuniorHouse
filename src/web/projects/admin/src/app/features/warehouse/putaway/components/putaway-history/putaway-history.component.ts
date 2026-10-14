import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PutawayRecordDto } from '../../models';

/** Recent putaways, newest first (PUT-14, PUT-15): what, where, how many, how it was identified, who and when. Read only: a mistake is an adjustment. */
@Component({
  selector: 'app-putaway-history',
  imports: [DatePipe],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (records().length === 0) {
      <p class="state-message">Nothing has been put away yet.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Recent putaways</caption>
        <thead>
          <tr>
            <th scope="col">When</th>
            <th scope="col">SKU</th>
            <th scope="col">Bin</th>
            <th scope="col">Quantity</th>
            <th scope="col">How</th>
            <th scope="col">Who</th>
          </tr>
        </thead>
        <tbody>
          @for (record of records(); track record.id) {
            <tr>
              <td data-label="When">{{ record.at | date: 'd MMM y, HH:mm' }}</td>
              <td data-label="SKU">{{ record.skuCode ?? '(no barcode yet)' }}</td>
              <td data-label="Bin">{{ record.binCode }}</td>
              <td data-label="Quantity">{{ record.quantity }}</td>
              <td data-label="How">{{ record.scanned ? 'Scanned' : 'Chosen from the list' }}</td>
              <td data-label="Who">{{ record.actorName }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PutawayHistoryComponent {
  readonly records = input.required<readonly PutawayRecordDto[]>();
  readonly loading = input.required<boolean>();
}
