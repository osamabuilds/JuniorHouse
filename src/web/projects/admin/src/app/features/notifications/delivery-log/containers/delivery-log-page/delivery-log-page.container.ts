import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { PaginationComponent, inputValue } from '@shared';
import { DeliveryDetailComponent, ResendRequest } from '../../components/delivery-detail/delivery-detail.component';
import { DeliverySummaryStripComponent } from '../../components/delivery-summary-strip/delivery-summary-strip.component';
import { DeliveryTableComponent } from '../../components/delivery-table/delivery-table.component';
import { CHANNEL_CHOICES, DeliveryFilters, KIND_CHOICES, NO_FILTERS, STATUS_CHOICES } from '../../models';
import {
  DeliveryLogPageActions,
  selectDetail,
  selectDetailLoading,
  selectError,
  selectFilters,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectResending,
  selectRows,
  selectSummary,
  selectTotal,
} from '../../store';

/**
 * The staff delivery log (docs/specs/notifications/delivery-log, DLG-1 to DLG-9, NFR-FT-07): every message to a customer with its status per
 * channel, a summary strip, filters (status, channel, kind, gave-up-and-not-resent) and a search by order number, email or mobile. A row opens
 * its attempts and the Resend action. `/notifications?orderId=7` shows one order's messages: the link from the order screen (DLG-9).
 */
@Component({
  selector: 'app-delivery-log-page',
  imports: [PaginationComponent, DeliveryTableComponent, DeliveryDetailComponent, DeliverySummaryStripComponent],
  templateUrl: './delivery-log-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliveryLogPageContainer {
  private readonly store = inject(Store);

  /** From the address `?orderId=` (bound by the router). */
  readonly orderId = input<string | undefined>(undefined);

  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly filters = this.store.selectSignal(selectFilters);
  readonly summary = this.store.selectSignal(selectSummary);
  readonly detail = this.store.selectSignal(selectDetail);
  readonly detailLoading = this.store.selectSignal(selectDetailLoading);
  readonly resending = this.store.selectSignal(selectResending);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly statuses = STATUS_CHOICES;
  readonly channels = CHANNEL_CHOICES;
  readonly kinds = KIND_CHOICES;
  readonly search = signal('');

  constructor() {
    effect(() => {
      const parsed = Number(this.orderId());
      this.store.dispatch(DeliveryLogPageActions.opened({ orderId: this.orderId() && Number.isInteger(parsed) && parsed > 0 ? parsed : null }));
    });
  }

  change(patch: Partial<DeliveryFilters>): void {
    this.store.dispatch(DeliveryLogPageActions.filtersChanged({ filters: { ...this.filters(), ...patch } }));
  }

  chooseStatus(event: Event): void {
    this.change({ status: inputValue(event) });
  }

  chooseChannel(event: Event): void {
    this.change({ channel: inputValue(event) });
  }

  chooseKind(event: Event): void {
    this.change({ kind: inputValue(event) });
  }

  toggleDeadLetters(event: Event): void {
    this.change({ deadLettersOnly: (event.target as HTMLInputElement).checked });
  }

  submitSearch(event: Event): void {
    event.preventDefault();
    this.change({ search: this.search() });
  }

  clear(): void {
    this.search.set('');
    this.store.dispatch(DeliveryLogPageActions.filtersChanged({ filters: NO_FILTERS }));
  }

  hasFilters(): boolean {
    const f = this.filters();
    return f.status !== '' || f.channel !== '' || f.kind !== '' || f.deadLettersOnly || f.orderId !== null || f.search !== '';
  }

  refresh(): void {
    this.store.dispatch(DeliveryLogPageActions.refreshed());
  }

  open(id: number): void {
    this.store.dispatch(DeliveryLogPageActions.detailOpened({ id }));
  }

  close(): void {
    this.store.dispatch(DeliveryLogPageActions.detailClosed());
  }

  resend(request: ResendRequest): void {
    this.store.dispatch(DeliveryLogPageActions.resendConfirmed(request));
  }

  dismissNotice(): void {
    this.store.dispatch(DeliveryLogPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(DeliveryLogPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(DeliveryLogPageActions.pageSizeChanged({ pageSize }));
  }
}
