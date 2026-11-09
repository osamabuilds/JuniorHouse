/** One step of the parcel's timeline: a status and when it happened (OHS-1). */
export interface TrackingStep {
  readonly statusCode: string;
  readonly statusName: string;
  readonly at: string;
}

/**
 * What a customer, or a stranger holding a tracking number, may see of a parcel (OHS-5, TRK-9): the courier, the waybill, the current status and
 * the timeline. There is no order data, address or staff name in it.
 */
export interface ShipmentTracking {
  readonly courierName: string;
  readonly waybill: string;
  readonly statusCode: string;
  readonly statusName: string;
  readonly steps: readonly TrackingStep[];
}

/** How a look-up by tracking number ended. Every miss is the same `notfound` (TRK-10, SEC-07). */
export type ParcelResult =
  | { readonly kind: 'found'; readonly tracking: ShipmentTracking }
  | { readonly kind: 'notfound' }
  | { readonly kind: 'captcha' }
  | { readonly kind: 'limited' }
  | { readonly kind: 'error' };
