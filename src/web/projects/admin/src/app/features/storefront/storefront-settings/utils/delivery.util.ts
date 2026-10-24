import { DeliveryRule, DraftSettings, Exchange } from '../models';

/** The Delivery screen's values: the city table and the exchange policy (window and conditions). */
export interface DeliveryValues {
  readonly rules: readonly DeliveryRule[];
  /** Blank until the owner sets one; a blank window with no conditions saves as no policy at all. */
  readonly windowDays: number | null;
  readonly conditions: readonly string[];
}

export function toDeliveryValues(draft: DraftSettings | null): DeliveryValues {
  return {
    rules: draft?.delivery ?? [],
    windowDays: draft?.exchange?.windowDays ?? null,
    conditions: draft?.exchange?.conditions ?? [],
  };
}

export function blankRule(): DeliveryRule {
  return { city: '', minDays: 1, maxDays: 3, charge: 0, freeOver: null, courier: '', enabled: true };
}

/** Empty conditions are dropped; with neither a window nor a condition there is no exchange policy to save. */
export function toExchange(values: DeliveryValues): Exchange | null {
  const conditions = values.conditions.map((c) => c.trim()).filter((c) => c.length > 0);
  if (values.windowDays === null && conditions.length === 0) {
    return null;
  }

  return { windowDays: values.windowDays ?? 0, conditions };
}

/** Reads a number box: blank is "not set", anything else is the number typed. */
export function parseAmount(text: string): number | null {
  const trimmed = text.trim();
  if (trimmed === '') {
    return null;
  }

  const value = Number(trimmed);
  return Number.isFinite(value) ? value : null;
}
