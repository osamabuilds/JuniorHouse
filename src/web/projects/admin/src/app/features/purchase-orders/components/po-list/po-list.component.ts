import { KeyValuePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ApiError } from '@core/http';
import { AppDatePipe, selectNumberOrNull } from '@shared';
import { VendorSummaryDto } from '@features/vendors';
import { PoSummaryDto } from '../../models';
import { PO_STATUS_DRAFT, PO_STATUS_LABELS } from '../../purchase-orders.constants';

/** The PO list with its vendor / status filters and the "Raise PO" action. */
@Component({
  selector: 'app-po-list',
  imports: [KeyValuePipe, AppDatePipe],
  templateUrl: './po-list.component.html',
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PoListComponent {
  readonly orders = input.required<readonly PoSummaryDto[]>();
  readonly vendors = input.required<readonly VendorSummaryDto[]>();
  readonly loading = input.required<boolean>();
  readonly error = input.required<ApiError | null>();
  readonly vendorFilter = input.required<number | null>();
  readonly statusFilter = input.required<number | null>();

  readonly vendorFilterChanged = output<number | null>();
  readonly statusFilterChanged = output<number | null>();
  readonly raiseRequested = output<void>();
  readonly viewRequested = output<PoSummaryDto>();

  readonly statusLabels = PO_STATUS_LABELS;
  readonly draftStatusId = PO_STATUS_DRAFT;

  onVendorFilterChange(event: Event): void {
    this.vendorFilterChanged.emit(selectNumberOrNull(event));
  }

  onStatusFilterChange(event: Event): void {
    this.statusFilterChanged.emit(selectNumberOrNull(event));
  }

  vendorName(vendorId: number): string {
    return this.vendors().find((vendor) => vendor.id === vendorId)?.name ?? `#${vendorId}`;
  }
}
