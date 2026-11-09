export interface SlipLine {
  readonly sku: string | null;
  readonly name: string;
  readonly colour: string;
  readonly size: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly lineTotal: number;
}

/**
 * The slip that goes in the parcel (SLP-1 to SLP-8). It shows the recipient name and city only, never the phone number or the email (SLP-7).
 * The exchange window and the support contact come from the shop settings, not from text in a template (SLP-4).
 */
export interface PackingSlip {
  readonly orderNumber: string;
  readonly placedAt: string;
  readonly recipientName: string;
  readonly city: string;
  readonly lines: readonly SlipLine[];
  readonly subtotal: number;
  readonly discount: number;
  readonly shipping: number;
  readonly total: number;
  readonly isCashOnDelivery: boolean;
  readonly cashToCollect: number;
  readonly isPaid: boolean;
  readonly brandName: string;
  readonly exchangeWindowDays: number;
  readonly supportWhatsapp: string | null;
  readonly supportEmail: string | null;
  readonly supportHours: string | null;
  readonly isReprint: boolean;
  readonly printCount: number;
  readonly printPending: boolean;
}

export interface PrintResult {
  readonly printed: boolean;
  readonly pending: boolean;
  readonly slip: PackingSlip;
}
