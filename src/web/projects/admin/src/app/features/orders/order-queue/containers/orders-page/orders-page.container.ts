import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { PaginationComponent, inputValue } from '@shared';
import { METHOD_CHOICES, NO_FILTERS, OrderFilters, OrderListItem, STATUS_CHOICES } from '../../models';
import {
  OrderQueueActions,
  selectError,
  selectFilters,
  selectLoading,
  selectOrders,
  selectPage,
  selectPageSize,
  selectTotal,
} from '../../store';

/**
 * The staff order queue (docs/specs/orders/order-queue, OQ-1, OQ-9): every order, newest first, with filters for status, payment method,
 * "call due now" and flagged orders, and a search by order number, mobile or name. Cash on delivery orders that need a call are easy to find.
 * Paged; the server answers one page at a time.
 */
@Component({
  selector: 'app-orders-page',
  imports: [RouterLink, DatePipe, DecimalPipe, PaginationComponent],
  templateUrl: './orders-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersPageContainer {
  private readonly store = inject(Store);

  readonly orders = this.store.selectSignal(selectOrders);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly filters = this.store.selectSignal(selectFilters);

  readonly statuses = STATUS_CHOICES;
  readonly methods = METHOD_CHOICES;
  readonly search = signal('');

  constructor() {
    this.store.dispatch(OrderQueueActions.listOpened());
  }

  change(patch: Partial<OrderFilters>): void {
    this.store.dispatch(OrderQueueActions.filtersChanged({ filters: { ...this.filters(), ...patch } }));
  }

  chooseStatus(event: Event): void {
    this.change({ status: Number(inputValue(event)) || null });
  }

  chooseMethod(event: Event): void {
    this.change({ method: Number(inputValue(event)) || null });
  }

  toggle(key: 'callDueNow' | 'flagged', event: Event): void {
    this.change({ [key]: (event.target as HTMLInputElement).checked });
  }

  submitSearch(event: Event): void {
    event.preventDefault();
    this.change({ search: this.search() });
  }

  clear(): void {
    this.search.set('');
    this.store.dispatch(OrderQueueActions.filtersChanged({ filters: NO_FILTERS }));
  }

  hasFilters(): boolean {
    const f = this.filters();
    return f.status !== null || f.method !== null || f.search !== '' || f.callDueNow || f.flagged;
  }

  isOverdue(order: OrderListItem): boolean {
    return order.statusId === 1 && order.codNextDueAt !== null && new Date(order.codNextDueAt).getTime() <= Date.now();
  }

  changePage(page: number): void {
    this.store.dispatch(OrderQueueActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(OrderQueueActions.pageSizeChanged({ pageSize }));
  }
}
