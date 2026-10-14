import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { BinRowDto } from '../../models';

/**
 * The bin list (BIN-6): one row per bin with its code, how many units it holds, when it was last counted, and an activate or
 * deactivate button. A bin that holds stock cannot be deactivated, so that button says why it is unavailable (BIN-4). Presentational only.
 */
@Component({
  selector: 'app-bin-table',
  imports: [DatePipe],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (bins().length === 0) {
      <p class="state-message">No bins found.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Bins</caption>
        <thead>
          <tr>
            <th scope="col">Bin</th>
            <th scope="col">Zone</th>
            <th scope="col">Aisle</th>
            <th scope="col">Shelf</th>
            <th scope="col">Units on hand</th>
            <th scope="col">Last counted</th>
            <th scope="col">Status</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (bin of bins(); track bin.id) {
            <tr>
              <td data-label="Bin"><strong>{{ bin.binCode }}</strong></td>
              <td data-label="Zone">{{ bin.zone }}</td>
              <td data-label="Aisle">{{ bin.aisle }}</td>
              <td data-label="Shelf">{{ bin.shelf }}</td>
              <td data-label="Units on hand">{{ bin.unitsOnHand }}</td>
              <td data-label="Last counted">
                @if (bin.lastCountedAt) {
                  {{ bin.lastCountedAt | date: 'd MMM y' }}
                } @else {
                  <span class="badge badge--muted">Never</span>
                }
              </td>
              <td data-label="Status">
                @if (bin.isActive) {
                  <span class="badge">Active</span>
                } @else {
                  <span class="badge badge--muted">Inactive</span>
                }
              </td>
              <td data-label="Actions">
                <button
                  type="button"
                  class="button button--small"
                  [disabled]="togglingId() === bin.id || (bin.isActive && bin.unitsOnHand > 0)"
                  [attr.title]="bin.isActive && bin.unitsOnHand > 0 ? 'This bin holds stock, so it cannot be deactivated.' : null"
                  (click)="toggle.emit(bin)"
                >
                  {{ bin.isActive ? 'Deactivate' : 'Activate' }}
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BinTableComponent {
  readonly bins = input.required<readonly BinRowDto[]>();
  readonly loading = input.required<boolean>();
  readonly togglingId = input<number | null>(null);

  readonly toggle = output<BinRowDto>();
}
