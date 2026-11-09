import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TrackedOrder } from '../../models/order-lookup.model';
import { ShipmentTrackingComponent } from '../shipment-tracking/shipment-tracking.component';

/**
 * The status page for one order (OLK-1, OLK-6): status first, then the items and totals. Presentational. It shows only what the server
 * sent, which is already masked: the mobile as 03XX-XXX1234 and the city, never the street or the recipient.
 */
@Component({
  selector: 'rp-tracked-order',
  imports: [DecimalPipe, DatePipe, ShipmentTrackingComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="rp-placed__box" [attr.aria-labelledby]="'tracked-' + order().orderNumber">
      <p class="rp-eyebrow">Order {{ order().orderNumber }}</p>
      <h2 class="rp-display-sm" [id]="'tracked-' + order().orderNumber">{{ order().statusName }}</h2>
      <p class="rp-help">
        Placed on <time [attr.datetime]="order().placedAt">{{ order().placedAt | date: 'd MMMM y' }}</time>. Delivering to {{ order().city }}. We message
        {{ order().maskedMobile }} as it moves along.
      </p>
      @if (order().tracking; as parcel) {
        <rp-shipment-tracking [tracking]="parcel" />
      } @else {
        <p class="rp-help">Your parcel is being prepared. Its tracking appears here once a courier has it.</p>
      }
      <h3 class="rp-display-sm">Your items</h3>
      <ul class="rp-sumlist">
        @for (line of order().lines; track $index) {
          <li>
            <span>{{ line.name }} <small>({{ line.colour }}, {{ line.size }}) × {{ line.quantity }}</small></span>
            <span>Rs. {{ line.lineTotal | number }}</span>
          </li>
        }
      </ul>
      <dl>
        <div class="rp-sumrow"><dt>Subtotal</dt><dd>Rs. {{ order().subtotal | number }}</dd></div>
        @if (order().discount > 0) {
          <div class="rp-sumrow rp-sumrow--sale"><dt>Discount</dt><dd>&minus; Rs. {{ order().discount | number }}</dd></div>
        }
        <div class="rp-sumrow"><dt>Delivery</dt><dd>{{ order().shipping === 0 ? 'Free' : 'Rs. ' + (order().shipping | number) }}</dd></div>
        <div class="rp-sumrow rp-sumrow--total"><dt>Total</dt><dd>Rs. {{ order().total | number }}</dd></div>
      </dl>
    </article>
  `,
})
export class TrackedOrderComponent {
  readonly order = input.required<TrackedOrder>();
}
