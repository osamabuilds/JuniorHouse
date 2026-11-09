import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { HasPermissionDirective } from '@core/auth';
import { TrackingTimelineComponent } from '../../../tracking';
import { errorWords } from '../../models';
import { ShipmentDetailPageActions, selectDetail, selectDetailLoading, selectError, selectNotice, selectRetrying } from '../../store';

/**
 * One shipment (docs/specs/shipping/tracking TRK-13): its status, courier and waybill, the booking history, and the full courier timeline. A parcel
 * waiting for a booking can be tried again now (MBQ-3); a booked one links to its label. No address or phone is shown here.
 */
@Component({
  selector: 'app-shipment-detail-page',
  imports: [RouterLink, CurrencyPipe, DatePipe, TrackingTimelineComponent, HasPermissionDirective],
  templateUrl: './shipment-detail-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShipmentDetailPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/dispatch/shipments/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly detail = this.store.selectSignal(selectDetail);
  readonly loading = this.store.selectSignal(selectDetailLoading);
  readonly retrying = this.store.selectSignal(selectRetrying);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (Number.isInteger(id) && id > 0) {
        this.store.dispatch(ShipmentDetailPageActions.opened({ id }));
      }
    });
  }

  reason(code: string | null): string {
    return errorWords(code);
  }

  retry(): void {
    this.store.dispatch(ShipmentDetailPageActions.retryClicked({ id: Number(this.id()) }));
  }

  dismissNotice(): void {
    this.store.dispatch(ShipmentDetailPageActions.noticeDismissed());
  }
}
