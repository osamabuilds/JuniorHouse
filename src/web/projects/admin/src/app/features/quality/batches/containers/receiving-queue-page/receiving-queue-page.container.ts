import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { PaginationComponent, inputValue, selectValue } from '@shared';
import { InboundTableComponent } from '../../components/inbound-table/inbound-table.component';
import { DELIVERY_STATUS_OPTIONS } from '../../batches.constants';
import {
  ReceivingQueueActions,
  selectDeliveries,
  selectDeliveriesError,
  selectDeliveriesLoading,
  selectDeliveriesPage,
  selectDeliveriesPageSize,
  selectDeliveriesTotal,
  selectDeliverySearch,
  selectDeliveryStatusFilter,
} from '../../store';

/**
 * Sprint 4: the receiving queue (BRD 5.11): every vendor delivery note recorded on a production run and whether the goods have been
 * counted yet. Filters change the search and go back to page 1.
 */
@Component({
  selector: 'app-receiving-queue-page',
  imports: [RouterLink, InboundTableComponent, PaginationComponent],
  template: `
    <h1>Receiving and quality control</h1>
    <p class="page-intro">
      When a vendor's delivery arrives, count it against the delivery note here. Each count opens a batch to inspect: accepted quantity
      is released, rejected quantity never is.
    </p>
    <p><a routerLink="/quality/batches">See all QC batches</a></p>

    <div class="toolbar">
      <label class="toolbar__search">
        <span class="sr-only">Search by purchase order or delivery note number</span>
        <input type="search" placeholder="Search by PO or delivery note number" [value]="searchText()" (input)="onSearchInput($event)" />
      </label>

      <label class="toolbar__select">
        <span class="sr-only">Delivery state</span>
        <select (change)="onStatusChange($event)">
          <option value="" [selected]="statusFilter() === null">All deliveries</option>
          @for (option of statuses; track option.value) {
            <option [value]="option.value" [selected]="statusFilter() === option.value">{{ option.label }}</option>
          }
        </select>
      </label>
    </div>

    @if (error()) {
      <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
    }

    <app-inbound-table [deliveries]="deliveries()" [loading]="loading()" />
    <app-pagination
      [total]="total()"
      [page]="currentPage()"
      [pageSize]="pageSize()"
      (pageChanged)="changePage($event)"
      (pageSizeChanged)="changePageSize($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReceivingQueuePageContainer {
  private readonly store = inject(Store);

  readonly deliveries = this.store.selectSignal(selectDeliveries);
  readonly total = this.store.selectSignal(selectDeliveriesTotal);
  readonly currentPage = this.store.selectSignal(selectDeliveriesPage);
  readonly pageSize = this.store.selectSignal(selectDeliveriesPageSize);
  readonly loading = this.store.selectSignal(selectDeliveriesLoading);
  readonly error = this.store.selectSignal(selectDeliveriesError);
  readonly searchText = this.store.selectSignal(selectDeliverySearch);
  readonly statusFilter = this.store.selectSignal(selectDeliveryStatusFilter);

  readonly statuses = DELIVERY_STATUS_OPTIONS;

  constructor() {
    this.store.dispatch(ReceivingQueueActions.opened());
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(ReceivingQueueActions.searchChanged({ searchText: inputValue(event) }));
  }

  onStatusChange(event: Event): void {
    this.store.dispatch(ReceivingQueueActions.statusFilterChanged({ status: selectValue(event) || null }));
  }

  changePage(page: number): void {
    this.store.dispatch(ReceivingQueueActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(ReceivingQueueActions.pageSizeChanged({ pageSize: size }));
  }
}
