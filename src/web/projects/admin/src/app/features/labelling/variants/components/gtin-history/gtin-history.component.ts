import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AppDatePipe } from '@shared';
import { VariantDetailDto } from '../../models';

/** Every barcode number one SKU has held (VAR-8): the active one and the retired ones with why, who and when. Presentational only. */
@Component({
  selector: 'app-gtin-history',
  imports: [AppDatePipe],
  template: `
    <section class="card" aria-labelledby="gh-heading">
      <h2 id="gh-heading">Barcode history for {{ detail().variant.skuCode }}</h2>
      <table class="responsive">
        <caption class="sr-only">Barcode numbers this SKU has held</caption>
        <thead>
          <tr>
            <th scope="col">Barcode</th>
            <th scope="col">Status</th>
            <th scope="col">Assigned</th>
            <th scope="col">Retired</th>
          </tr>
        </thead>
        <tbody>
          @for (gtin of detail().gtins; track gtin.id) {
            <tr>
              <td data-label="Barcode">{{ gtin.gtin }}</td>
              <td data-label="Status">
                <span class="badge" [class.badge--muted]="gtin.statusCode === 'Retired'">{{ gtin.statusCode === 'Active' ? 'In use' : 'Retired' }}</span>
              </td>
              <td data-label="Assigned">{{ gtin.assignedAt | appDate: 'datetime' }}</td>
              <td data-label="Retired">
                @if (gtin.retiredAt) {
                  {{ gtin.retiredAt | appDate: 'datetime' }} by {{ gtin.retiredBy }}<br />
                  <small>Reason: {{ gtin.retiredReasonCode }}{{ gtin.retiredNote ? ' (' + gtin.retiredNote + ')' : '' }}</small>
                } @else {
                  —
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
      <div class="form-actions">
        <button type="button" class="button" (click)="closed.emit()">Close</button>
      </div>
    </section>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GtinHistoryComponent {
  readonly detail = input.required<VariantDetailDto>();

  readonly closed = output<void>();
}
