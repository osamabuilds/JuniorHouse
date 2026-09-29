import { PoDto, PoLineDto } from './po.model';

export interface PoRevisionCommunicationDto {
  readonly typeId: number;
  readonly channelId: number;
  readonly responderName: string;
  readonly responseDte: string;
  readonly evidence: readonly { readonly fileId: number; readonly fileName: string }[] | null;
}

export interface PoRevisionDto {
  readonly id: number;
  readonly poId: number;
  readonly revisionNumber: number;
  readonly initiatorId: number;
  readonly statusId: number;
  readonly reasonId: number;
  readonly impactNote: string;
  readonly vendorMessage: string | null;
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
  readonly paymentTermId: number;
  readonly advancePercent: number;
  readonly fabricResponsibilityId: number | null;
  readonly poValueBefore: number;
  readonly poValueAfter: number;
  readonly poValueDiff: number;
  readonly advanceAmountBefore: number;
  readonly advanceAmountAfter: number;
  readonly expectedDateShiftDays: number;
  readonly latestAcceptableDateShiftDays: number | null;
  readonly quantityDiff: number;
  readonly isBeyondLatestAcceptableDate: boolean;
  readonly lines: readonly PoLineDto[];
  readonly communications: readonly PoRevisionCommunicationDto[] | null;
}

/** A vendor-visible file an amendment introduces; `content` is the file's bytes (the API's `byte[]`). */
export interface AmendmentFileAdd {
  readonly categoryId: number;
  readonly fileName: string;
  readonly content: string;
}

export interface TermsValue {
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
  readonly paymentTermId: number;
  readonly advancePercent: number;
  readonly fabricResponsibilityId: number | null;
  readonly lines: readonly PoLineDto[];
}

export interface AmendmentValue extends TermsValue {
  readonly initiatorId: number;
  readonly reasonId: number;
  readonly impactNote: string;
  readonly vendorMessage: string | null;
  readonly addFiles?: readonly AmendmentFileAdd[];
  readonly retireFileIds?: readonly number[];
}

export interface CounterProposalValue extends TermsValue {
  readonly reasonId: number;
  readonly impactNote: string;
  readonly vendorMessage: string | null;
}

export interface VendorResponseValue {
  readonly outcomeTypeId: number;
  readonly revisionNumber: number;
  readonly channelId: number;
  readonly responderName: string;
  readonly responseDte: string | null;
  readonly counter: CounterProposalValue | null;
  /** Optional proof of what the vendor said (e.g. a WhatsApp screenshot); kept as internal files. */
  readonly evidence?: readonly { readonly fileName: string; readonly content: string }[];
}

export interface VendorResponseResult {
  readonly outcomeTypeId: number;
  readonly po: PoDto;
  readonly revision: PoRevisionDto | null;
  readonly suggestedCancelReasonId: number | null;
}

/** Accept / reject / withdraw of the PO's one open Pending revision. */
export interface RevisionDecision {
  readonly kind: 'accept' | 'reject' | 'withdraw';
  readonly poId: number;
  readonly revisionNumber: number;
  readonly note: string | null;
}
