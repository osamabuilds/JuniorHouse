import {
  PO_STATUS_ACKNOWLEDGED,
  PO_STATUS_DELIVERED,
  PO_STATUS_DRAFT,
  PO_STATUS_IN_PRODUCTION,
  PO_STATUS_PARTIALLY_DELIVERED,
  PO_STATUS_SENT_TO_VENDOR,
} from '../purchase-orders.constants';
import { PoDto } from '../models';

export function canEdit(po: PoDto): boolean {
  return po.statusId === PO_STATUS_DRAFT;
}

export function canSend(po: PoDto): boolean {
  return po.statusId === PO_STATUS_DRAFT;
}

/** AC-25..AC-31: a vendor response is recorded against a PO that has been sent and not yet acknowledged. */
export function canRecordResponse(po: PoDto): boolean {
  return po.statusId === PO_STATUS_SENT_TO_VENDOR;
}

/** True from the moment the vendor has acknowledged the order until it is delivered: production tracking applies. */
export function isInProductionStage(po: PoDto): boolean {
  return po.statusId === PO_STATUS_ACKNOWLEDGED || po.statusId === PO_STATUS_IN_PRODUCTION || po.statusId === PO_STATUS_PARTIALLY_DELIVERED;
}

/** AC-9/AC-10, Sprint 3 AC-26: a sent order is amended until it is delivered; a Draft is simply edited. */
export function canAmend(po: PoDto): boolean {
  return po.statusId === PO_STATUS_SENT_TO_VENDOR || isInProductionStage(po);
}

/** Sprint 3 AC-28: cancelling is allowed at any point before Delivered, including after production has started. */
export function canCancel(po: PoDto): boolean {
  return po.statusId === PO_STATUS_DRAFT || po.statusId === PO_STATUS_SENT_TO_VENDOR || isInProductionStage(po);
}

/** Cancelling now wastes work the vendor has already done: staff get a prominent warning first (D5). */
export function cancelNeedsProductionWarning(po: PoDto): boolean {
  return po.statusId === PO_STATUS_IN_PRODUCTION || po.statusId === PO_STATUS_PARTIALLY_DELIVERED;
}

/** Sprint 3 AC-25 (D2): closing is manual, and only once the order is Delivered. */
export function canClose(po: PoDto): boolean {
  return po.statusId === PO_STATUS_DELIVERED;
}

/** One plain sentence telling staff where this PO stands and what they can do next. */
export function statusHelp(po: PoDto): string {
  switch (po.statusId) {
    case PO_STATUS_DRAFT:
      return 'This order is still a draft, so only you can see it. Check the details, attach the tech pack, then send it to the vendor.';
    case PO_STATUS_SENT_TO_VENDOR:
      return 'This order has been sent and is waiting for the vendor. Record what the vendor says, or amend the order if something changes.';
    case PO_STATUS_ACKNOWLEDGED:
      return 'The vendor has confirmed this order. From now on, any change to the price, dates or quantities has to be agreed as an amendment. Track its progress under Production.';
    case PO_STATUS_IN_PRODUCTION:
      return 'Bulk cutting has started. Record delivery notes under Production as the vendor ships. Changes to the order still go through an amendment, and cannot reduce what has already been delivered.';
    case PO_STATUS_PARTIALLY_DELIVERED:
      return 'The vendor has delivered part of this order. Record each further delivery note under Production.';
    case PO_STATUS_DELIVERED:
      return 'The order has been delivered. Check the delivery notes under Production, then close the order when you are satisfied. Closing is always your decision.';
    default:
      return po.statusId === 8
        ? 'This order is closed. Its details, delivery notes and files are kept for the record.'
        : 'This order is cancelled and closed. Its details and files are kept for the record.';
  }
}
