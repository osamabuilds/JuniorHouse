/** One pick exception as the staff queue shows it (PEX-4): order number, SKU, bin, the system count, the count found, the picker and the time. */
export interface ExceptionRow {
  readonly id: number;
  readonly orderId: number;
  readonly orderNumber: string;
  readonly sku: string | null;
  readonly binCode: string;
  readonly reason: string;
  readonly status: string;
  readonly systemCount: number;
  readonly foundCount: number;
  readonly raisedBy: string;
  readonly raisedAt: string;
  readonly resolvedBy: string | null;
  readonly resolvedAt: string | null;
}

/** What the picker types into the raise form (PEX-5). */
export interface RaiseExceptionRequest {
  readonly variantId: number;
  readonly binCode: string;
  readonly reason: string;
  readonly foundCount: number;
}

/** How staff close an exception (PEX-8, PEX-9). */
export type ResolveAction = 'restocked' | 'cancel-line';

export interface ReasonChoice {
  readonly code: string;
  readonly name: string;
}

export const REASON_CHOICES: readonly ReasonChoice[] = [
  { code: 'NOT_FOUND', name: 'Not on the shelf' },
  { code: 'SHORT', name: 'Fewer than the system says' },
  { code: 'DAMAGED', name: 'Damaged' },
];

export function reasonName(code: string): string {
  return REASON_CHOICES.find((r) => r.code === code)?.name ?? code;
}
