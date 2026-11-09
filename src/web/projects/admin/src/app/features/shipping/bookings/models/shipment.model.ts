import { TrackingEvent } from '../../tracking';

/** One shipment in the staff list: the order number, status, courier and waybill, and what needs attention. */
export interface ShipmentRow {
  readonly id: number;
  readonly orderId: number;
  readonly orderNumber: string;
  readonly status: string;
  readonly statusName: string;
  readonly courierName: string | null;
  readonly waybill: string | null;
  readonly statusAt: string;
  readonly failedAttempts: number;
  readonly needsStaff: boolean;
  readonly cashToCollect: number;
}

/** A shipment with its booking history and the courier timeline (TRK-13). No address or phone: the label reads those from Orders. */
export interface ShipmentDetail {
  readonly row: ShipmentRow;
  readonly bookingSource: string | null;
  readonly bookedAt: string | null;
  readonly attempts: number;
  readonly lastError: string | null;
  readonly lastNote: string | null;
  readonly weightGrams: number;
  readonly manifestId: number | null;
  readonly labelPrints: number;
  readonly events: readonly TrackingEvent[];
}

export interface StatusChoice {
  readonly code: string;
  readonly name: string;
}

export const STATUS_CHOICES: readonly StatusChoice[] = [
  { code: 'PENDING_BOOKING', name: 'Pending booking' },
  { code: 'BOOKED', name: 'Booked' },
  { code: 'PICKED_UP', name: 'Picked up' },
  { code: 'IN_TRANSIT', name: 'In transit' },
  { code: 'OUT_FOR_DELIVERY', name: 'Out for delivery' },
  { code: 'FAILED_ATTEMPT', name: 'Failed attempt' },
  { code: 'DELIVERED', name: 'Delivered' },
  { code: 'RETURNED_TO_ORIGIN', name: 'Returned to origin' },
  { code: 'CANCELLED', name: 'Cancelled' },
];

export function errorWords(code: string | null): string {
  return code === 'UNREACHABLE' ? 'No courier could be reached' : code === 'REJECTED' ? 'A courier refused the parcel' : '';
}
