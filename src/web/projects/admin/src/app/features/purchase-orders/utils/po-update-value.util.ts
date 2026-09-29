import { PoDto, PoFormDraft, UpdatePoValue } from '../models';

/**
 * The commercial terms (latest acceptable date, tolerances, fabric responsibility) are set on
 * update; the create call takes only the core fields. A blank payment term / advance falls back to
 * the PO's own.
 */
export function toUpdatePoValue(draft: PoFormDraft, po: Pick<PoDto, 'paymentTermId' | 'advancePercent'>): UpdatePoValue {
  return {
    unitCost: draft.unitCost,
    expectedDeliveryDate: draft.expectedDeliveryDate,
    paymentTermId: draft.paymentTermId ?? po.paymentTermId,
    advancePercent: draft.advancePercent ?? po.advancePercent,
    lines: draft.lines,
    latestAcceptableDate: draft.latestAcceptableDate,
    overTolerancePercent: draft.overTolerancePercent,
    underTolerancePercent: draft.underTolerancePercent,
    fabricResponsibilityId: draft.fabricResponsibilityId,
  };
}

/** Whether the draft sets any of the terms that only the update call accepts. */
export function hasCommercialTerms(draft: PoFormDraft): boolean {
  return (
    !!draft.latestAcceptableDate ||
    draft.overTolerancePercent !== null ||
    draft.underTolerancePercent !== null ||
    draft.fabricResponsibilityId !== null
  );
}
