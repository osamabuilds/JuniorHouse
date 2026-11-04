import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { HasPermissionDirective } from '@core/auth';
import { CodOutcomeChoice } from '../../models';
import { OrderQueueActions, selectActing, selectDetail, selectDetailLoading, selectError, selectNotice } from '../../store';

/**
 * One order for staff (docs/specs/orders/order-queue, OQ-2 to OQ-7): who ordered, where it goes, what is in it, what it costs, every step it
 * has been through and every confirmation call. Cash on delivery orders waiting for a call offer the three outcomes; any order that has not
 * been dispatched can be cancelled. Buttons need `orders.manage`; the server checks it again on every request.
 */
@Component({
  selector: 'app-order-detail-page',
  imports: [RouterLink, DatePipe, DecimalPipe, HasPermissionDirective],
  templateUrl: './order-detail-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetailPageContainer {
  private readonly store = inject(Store);

  /** The order id from the address (bound by the router). */
  readonly id = input.required<string>();

  readonly order = this.store.selectSignal(selectDetail);
  readonly loading = this.store.selectSignal(selectDetailLoading);
  readonly acting = this.store.selectSignal(selectActing);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly note = signal('');
  readonly confirmingCancel = signal(false);

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (Number.isFinite(id)) this.store.dispatch(OrderQueueActions.detailOpened({ id }));
    });
  }

  record(outcome: CodOutcomeChoice): void {
    const order = this.order();
    if (!order) return;
    this.store.dispatch(OrderQueueActions.outcomeRecorded({ id: order.id, outcome, note: this.note() }));
    this.note.set('');
  }

  askCancel(): void {
    this.confirmingCancel.set(true);
  }

  keep(): void {
    this.confirmingCancel.set(false);
  }

  cancel(): void {
    const order = this.order();
    if (!order) return;
    this.store.dispatch(OrderQueueActions.orderCancelled({ id: order.id, note: this.note() }));
    this.confirmingCancel.set(false);
    this.note.set('');
  }

  clearFlag(): void {
    const order = this.order();
    if (order) this.store.dispatch(OrderQueueActions.flagCleared({ id: order.id }));
  }

  dismissNotice(): void {
    this.store.dispatch(OrderQueueActions.noticeDismissed());
  }
}
