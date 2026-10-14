import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PaginationComponent } from '@shared';
import { MOVEMENT_KIND_TEXT } from '../../stock-ledger.constants';
import { StockMovementDto, StockVariantRowDto } from '../../models';

/**
 * The ledger history of one variant, newest first (LED-9): kind, bin, grade, the change, the balance before and after, who and when.
 * Nothing here can be edited: corrections are new movements. Presentational only.
 */
@Component({
  selector: 'app-movement-list',
  imports: [DatePipe, PaginationComponent],
  template: `
    <section class="card" [attr.aria-labelledby]="'ml-heading'">
      <h2 id="ml-heading">Movements for {{ variant().skuCode }}</h2>
      @if (loading()) {
        <p class="state-message">Loading…</p>
      } @else if (movements().length === 0) {
        <p class="state-message">No movements yet.</p>
      } @else {
        <table class="responsive">
          <caption class="sr-only">Ledger movements for {{ variant().skuCode }}</caption>
          <thead>
            <tr>
              <th scope="col">When</th>
              <th scope="col">What</th>
              <th scope="col">Bin</th>
              <th scope="col">Change</th>
              <th scope="col">Before → after</th>
              <th scope="col">Who</th>
            </tr>
          </thead>
          <tbody>
            @for (m of movements(); track m.id) {
              <tr>
                <td data-label="When">{{ m.at | date: 'd MMM y, HH:mm' }}</td>
                <td data-label="What">{{ kindText(m.kindCode) }} <small>({{ m.gradeCode === 'CONCESSION' ? 'concession' : 'accepted' }})</small></td>
                <td data-label="Bin">{{ m.binCode }}</td>
                <td data-label="Change"><strong>{{ m.quantityChange > 0 ? '+' : '' }}{{ m.quantityChange }}</strong></td>
                <td data-label="Before → after">{{ m.balanceBefore }} → {{ m.balanceAfter }}</td>
                <td data-label="Who">{{ m.actorName }}</td>
              </tr>
            }
          </tbody>
        </table>
        <app-pagination [total]="total()" [page]="page()" [pageSize]="pageSize()" (pageChanged)="pageChanged.emit($event)" />
      }
      <div class="form-actions">
        <button type="button" class="button" (click)="closed.emit()">Close</button>
      </div>
    </section>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MovementListComponent {
  readonly variant = input.required<StockVariantRowDto>();
  readonly movements = input.required<readonly StockMovementDto[]>();
  readonly loading = input(false);
  readonly total = input(0);
  readonly page = input(1);
  readonly pageSize = input(10);

  readonly pageChanged = output<number>();
  readonly closed = output<void>();

  kindText(code: string): string {
    return MOVEMENT_KIND_TEXT[code] ?? code;
  }
}
