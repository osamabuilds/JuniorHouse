import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { PaginationComponent, inputValue } from '@shared';
import { ShipmentsTableComponent } from '../../components/shipments-table/shipments-table.component';
import { STATUS_CHOICES } from '../../models';
import { ShipmentsPageActions, selectError, selectLoading, selectPage, selectPageSize, selectRows, selectStatus, selectTotal } from '../../store';

/**
 * The shipments (docs/specs/shipping/bookings and tracking): every parcel with its courier, waybill and status, newest first and paged, filtered by
 * status. A failed attempt or a parcel waiting for staff is marked in words. A row opens the shipment with its full timeline.
 */
@Component({
  selector: 'app-shipments-page',
  imports: [PaginationComponent, RouterLink, ShipmentsTableComponent],
  templateUrl: './shipments-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShipmentsPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);

  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly status = this.store.selectSignal(selectStatus);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly statuses = STATUS_CHOICES;

  constructor() {
    this.store.dispatch(ShipmentsPageActions.opened());
  }

  chooseStatus(event: Event): void {
    this.store.dispatch(ShipmentsPageActions.statusChosen({ status: inputValue(event) }));
  }

  open(id: number): void {
    void this.router.navigate(['/dispatch/shipments', id]);
  }

  changePage(page: number): void {
    this.store.dispatch(ShipmentsPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(ShipmentsPageActions.pageSizeChanged({ pageSize }));
  }

  refresh(): void {
    this.store.dispatch(ShipmentsPageActions.refreshed());
  }
}
