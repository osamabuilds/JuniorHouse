import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LookupNameResolver } from '@features/reference/lookups';
import { AppDatePipe } from '@shared';
import { PoReceiptProgressDto } from '../../models';

/**
 * What has been received against a purchase order (Sprint 4, spec AC-40): per size and colour, ordered against counted, and the goods
 * receipts behind it. It is receipt progress only; it never changes the order's status (closing an order stays a manual step).
 */
@Component({
  selector: 'app-po-receipts-panel',
  imports: [RouterLink, AppDatePipe],
  template: `
    <section class="panel" aria-labelledby="po-receipts-heading">
      <h3 id="po-receipts-heading">Goods received</h3>
      <p>
        {{ progress().receivedTotal }} of {{ progress().orderedTotal }} pieces counted in so far.
        <a routerLink="/quality/batches" [queryParams]="{ search: poNo() }">See the QC batches</a>
      </p>

      <table class="responsive">
        <caption class="sr-only">
          Received against ordered
        </caption>
        <thead>
          <tr>
            <th scope="col">Size</th>
            <th scope="col">Colour</th>
            <th scope="col">Ordered</th>
            <th scope="col">Received</th>
            <th scope="col">Still to come</th>
          </tr>
        </thead>
        <tbody>
          @for (line of progress().lines; track line.sizeId + '-' + line.colourId) {
            <tr>
              <td data-label="Size">{{ names().sizeName(line.sizeId) }}</td>
              <td data-label="Colour">{{ names().colourName(line.colourId) }}</td>
              <td data-label="Ordered">{{ line.orderedQty }}</td>
              <td data-label="Received">{{ line.receivedQty }}</td>
              <td data-label="Still to come">{{ line.outstandingQty }}</td>
            </tr>
          }
        </tbody>
      </table>

      @if (progress().receipts.length > 0) {
        <ul>
          @for (receipt of progress().receipts; track receipt.grnId) {
            <li>{{ receipt.grnNo }}: {{ receipt.countedTotal }} counted of {{ receipt.claimedTotal }} claimed, on {{ receipt.receivedDate | appDate }}</li>
          }
        </ul>
      }
    </section>
  `,
  styles: ':host { display: block; margin-top: var(--space-4); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PoReceiptsPanelComponent {
  readonly progress = input.required<PoReceiptProgressDto>();
  readonly names = input.required<LookupNameResolver>();
  readonly poNo = input.required<string>();
}
