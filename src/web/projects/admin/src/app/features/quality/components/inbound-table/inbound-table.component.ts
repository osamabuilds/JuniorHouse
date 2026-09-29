import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppDatePipe } from '@shared';
import { InboundDeliverySummaryDto } from '../../models';

/** The receiving queue (spec AC-1, AC-5, AC-44): one row per vendor delivery note, with what state it is in written out. */
@Component({
  selector: 'app-inbound-table',
  imports: [RouterLink, AppDatePipe],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (deliveries().length === 0) {
      <p class="state-message">
        Nothing here. A delivery note appears in this queue as soon as it is recorded on a production run.
      </p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">
          Vendor delivery notes
        </caption>
        <thead>
          <tr>
            <th scope="col">Purchase order</th>
            <th scope="col">Delivery note</th>
            <th scope="col">Dispatched</th>
            <th scope="col">Vendor says</th>
            <th scope="col">State</th>
            <th scope="col"><span class="sr-only">Action</span></th>
          </tr>
        </thead>
        <tbody>
          @for (delivery of deliveries(); track delivery.id) {
            <tr>
              <td data-label="Purchase order">{{ delivery.poNo }}</td>
              <td data-label="Delivery note">
                {{ delivery.vendorDnNo }}
                @if (delivery.isFinalShipment) {
                  <span class="badge badge--muted">Final shipment</span>
                }
              </td>
              <td data-label="Dispatched">{{ delivery.dispatchDate | appDate }}</td>
              <td data-label="Vendor says">{{ delivery.claimedTotal }} pieces</td>
              <td data-label="State">
                @if (delivery.isCancelled) {
                  <span class="qc-flag qc-flag--danger">Purchase order cancelled</span>
                } @else if (delivery.grnNo) {
                  <span class="qc-flag qc-flag--ok">Received: {{ delivery.grnNo }}</span>
                } @else {
                  <span class="qc-flag qc-flag--warn">Waiting to be received</span>
                }
              </td>
              <td data-label="Action" class="actions-cell">
                @if (!delivery.isCancelled && !delivery.grnNo) {
                  <a class="button button--small" [routerLink]="['/quality/receive', delivery.id]">Receive goods</a>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InboundTableComponent {
  readonly deliveries = input.required<readonly InboundDeliverySummaryDto[]>();
  readonly loading = input.required<boolean>();
}
