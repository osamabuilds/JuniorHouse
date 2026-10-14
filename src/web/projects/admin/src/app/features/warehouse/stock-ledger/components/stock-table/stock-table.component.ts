import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { StockVariantRowDto } from '../../models';

/**
 * Stock per variant (LED-5, LED-6): on hand split by grade, available, and every bin the variant sits in with the quantity, so pick
 * staff know where to go. A retired barcode is flagged because putaway refuses it. Presentational only.
 */
@Component({
  selector: 'app-stock-table',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (rows().length === 0) {
      <p class="state-message">No stock found.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Stock by variant</caption>
        <thead>
          <tr>
            <th scope="col">SKU</th>
            <th scope="col">Barcode</th>
            <th scope="col">On hand</th>
            <th scope="col">Available</th>
            <th scope="col">Where it is</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track row.variantRefId) {
            <tr>
              <td data-label="SKU"><strong>{{ row.skuCode }}</strong></td>
              <td data-label="Barcode">
                {{ row.gtin }}
                @if (row.gtinRetired) {
                  <br /><span class="badge badge--muted">Barcode retired</span>
                }
              </td>
              <td data-label="On hand">
                {{ row.onHand }}
                <br /><small>{{ row.onHandAccepted }} accepted, {{ row.onHandConcession }} concession</small>
              </td>
              <td data-label="Available">{{ row.available }}</td>
              <td data-label="Where it is">
                @if (row.bins.length === 0) {
                  <span class="badge badge--muted">In no bin</span>
                } @else {
                  <ul class="plain-list">
                    @for (bin of row.bins; track bin.binId + '-' + bin.gradeId) {
                      <li>{{ bin.binCode }}: {{ bin.quantity }} <small>({{ bin.gradeCode === 'CONCESSION' ? 'concession' : 'accepted' }})</small></li>
                    }
                  </ul>
                }
              </td>
              <td data-label="Actions">
                <button type="button" class="button button--small" (click)="history.emit(row)">Movements</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; } small { color: var(--color-muted, #555); } .plain-list { list-style: none; margin: 0; padding: 0; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockTableComponent {
  readonly rows = input.required<readonly StockVariantRowDto[]>();
  readonly loading = input.required<boolean>();

  readonly history = output<StockVariantRowDto>();
}
