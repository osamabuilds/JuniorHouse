import {
  PO_STATUS_ACKNOWLEDGED,
  PO_STATUS_DRAFT,
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

/** AC-9/AC-10: only a sent or acknowledged PO is amended; a Draft is simply edited. */
export function canAmend(po: PoDto): boolean {
  return po.statusId === PO_STATUS_SENT_TO_VENDOR || po.statusId === PO_STATUS_ACKNOWLEDGED;
}

export function canCancel(po: PoDto): boolean {
  return po.statusId === PO_STATUS_DRAFT || po.statusId === PO_STATUS_SENT_TO_VENDOR || po.statusId === PO_STATUS_ACKNOWLEDGED;
}

/** One plain sentence telling staff where this PO stands and what they can do next. */
export function statusHelp(po: PoDto): string {
  switch (po.statusId) {
    case PO_STATUS_DRAFT:
      return 'This order is still a draft, so only you can see it. Check the details, attach the tech pack, then send it to the vendor.';
    case PO_STATUS_SENT_TO_VENDOR:
      return 'This order has been sent and is waiting for the vendor. Record what the vendor says, or amend the order if something changes.';
    case PO_STATUS_ACKNOWLEDGED:
      return 'The vendor has confirmed this order. From now on, any change to the price, dates or quantities has to be agreed as an amendment.';
    default:
      return 'This order is cancelled and closed. Its details and files are kept for the record.';
  }
}
