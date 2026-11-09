import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ShipmentRow } from '../../models';
import { needsAttention } from '../../../tracking';

/** The shipments, newest first: order number, courier and waybill, status, and what needs attention in words. Presentational: it only reports a click. */
@Component({
  selector: 'app-shipments-table',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './shipments-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShipmentsTableComponent {
  readonly rows = input.required<readonly ShipmentRow[]>();

  readonly open = output<number>();

  attention(row: ShipmentRow): boolean {
    return needsAttention(row.status) || row.needsStaff;
  }
}
