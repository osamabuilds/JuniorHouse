import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { PaginationComponent } from '@shared';
import { COURIER_CHOICES, reasonWords, waitingText } from '../../models';
import {
  QueuePageActions,
  selectBusyId,
  selectError,
  selectLoading,
  selectNeedsStaffOnly,
  selectNotice,
  selectPage,
  selectPageSize,
  selectRows,
  selectTotal,
} from '../../store';

/**
 * The manual booking queue (docs/specs/shipping/manual-booking-queue, MBQ-1 to MBQ-8): parcels no courier would take, oldest first, with why and
 * for how long. Staff try again now, or enter a waybill they got by phone. The parcels wait in the pending-booking area, never shipped unlabelled.
 */
@Component({
  selector: 'app-queue-page',
  imports: [RouterLink, PaginationComponent],
  templateUrl: './queue-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueuePageContainer {
  private readonly store = inject(Store);

  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly needsStaffOnly = this.store.selectSignal(selectNeedsStaffOnly);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly busyId = this.store.selectSignal(selectBusyId);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly couriers = COURIER_CHOICES;
  readonly entering = signal<number | null>(null);
  readonly courier = signal('PRIMARY');
  readonly waybill = signal('');

  constructor() {
    this.store.dispatch(QueuePageActions.opened());
  }

  words = reasonWords;
  wait = waitingText;

  toggleStaffOnly(event: Event): void {
    this.store.dispatch(QueuePageActions.filterChanged({ needsStaffOnly: (event.target as HTMLInputElement).checked }));
  }

  retry(id: number): void {
    this.store.dispatch(QueuePageActions.retryClicked({ id }));
  }

  submitWaybill(event: Event, id: number): void {
    event.preventDefault();
    const waybill = this.waybill().trim();
    if (waybill) {
      this.store.dispatch(QueuePageActions.waybillEntered({ id, courier: this.courier(), waybill }));
      this.entering.set(null);
      this.waybill.set('');
    }
  }

  type(field: 'courier' | 'waybill', event: Event): void {
    this[field].set((event.target as HTMLInputElement | HTMLSelectElement).value);
  }

  changePage(page: number): void {
    this.store.dispatch(QueuePageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(QueuePageActions.pageSizeChanged({ pageSize }));
  }

  refresh(): void {
    this.store.dispatch(QueuePageActions.refreshed());
  }

  dismissNotice(): void {
    this.store.dispatch(QueuePageActions.noticeDismissed());
  }
}
