import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ShipmentTracking } from '../../models/shipment-tracking.model';

/**
 * Where the parcel is (OHS-1, OHS-3, TRK-9): the courier and waybill, the current status in words, and the timeline newest first. Presentational.
 * A failed delivery attempt is said in words, not only by colour. It shows only what the server sent, which holds no order data (OHS-5).
 */
@Component({
  selector: 'rp-shipment-tracking',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rp-placed__box" aria-labelledby="parcel-heading">
      <h3 class="rp-display-sm" id="parcel-heading">Your parcel</h3>
      <p>
        <b>{{ tracking().statusName }}</b>
        @if (tracking().statusCode === 'FAILED_ATTEMPT') {
          <span> The courier could not deliver it. They will try again.</span>
        }
      </p>
      <p class="rp-help">With {{ tracking().courierName }}. Tracking number {{ tracking().waybill }}.</p>
      <ol class="rp-sumlist">
        @for (step of newestFirst(); track $index) {
          <li>
            <span>{{ step.statusName }}</span>
            <time [attr.datetime]="step.at">{{ step.at | date: 'd MMM y, h:mm a' }}</time>
          </li>
        }
      </ol>
    </section>
  `,
})
export class ShipmentTrackingComponent {
  readonly tracking = input.required<ShipmentTracking>();

  protected newestFirst() {
    return [...this.tracking().steps].reverse();
  }
}
