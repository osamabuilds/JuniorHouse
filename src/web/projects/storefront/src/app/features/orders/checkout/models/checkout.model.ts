/** One line of the basket as the server priced it for checkout. Every figure is the server's (SEC-09). */
export interface CheckoutLine {
  readonly variantId: number;
  readonly productId: number;
  readonly name: string;
  readonly path: string;
  readonly sku: string | null;
  readonly colour: string;
  readonly size: string;
  readonly unitPrice: number;
  readonly quantity: number;
  readonly lineTotal: number;
}

export interface PaymentMethod {
  readonly code: string;
  readonly id: number;
  readonly name: string;
  readonly isOnline: boolean;
}

export interface SavedAddress {
  readonly id: number;
  readonly recipientName: string;
  readonly phone: string;
  readonly provinceId: number;
  readonly cityId: number;
  readonly lineOne: string;
  readonly lineTwo: string | null;
  readonly postcode: string | null;
  readonly isDefault: boolean;
}

/** What the checkout page shows. `shipping` is null until a city is chosen ("calculated at checkout"). */
export interface CheckoutSummary {
  readonly lines: readonly CheckoutLine[];
  readonly subtotal: number;
  readonly discount: number;
  readonly shipping: number | null;
  readonly total: number;
  readonly canOrder: boolean;
  readonly problems: readonly string[];
  readonly paymentMethods: readonly PaymentMethod[];
  readonly savedAddresses: readonly SavedAddress[];
}

export interface Province {
  readonly id: number;
  readonly code: string;
  readonly name: string;
}

export interface City {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly provinceId: number;
}

/** What the shopper typed. No price, total or quantity is part of it: the server knows the basket (SEC-09). */
export interface CheckoutForm {
  readonly name: string;
  readonly email: string;
  readonly mobile: string;
  readonly whatsAppOptIn: boolean;
  readonly savedAddressId: number | null;
  readonly recipientName: string;
  readonly recipientPhone: string;
  readonly lineOne: string;
  readonly lineTwo: string;
  readonly provinceId: number;
  readonly cityId: number;
  readonly postcode: string;
  readonly note: string;
  readonly paymentMethod: string;
}

export const EMPTY_FORM: CheckoutForm = {
  name: '',
  email: '',
  mobile: '',
  whatsAppOptIn: true,
  savedAddressId: null,
  recipientName: '',
  recipientPhone: '',
  lineOne: '',
  lineTwo: '',
  provinceId: 0,
  cityId: 0,
  postcode: '',
  note: '',
  paymentMethod: 'COD',
};

/** Field name (as the form uses it) to the message that says what to do. */
export type FieldErrors = Readonly<Record<string, string>>;

/** Every way placing an order can end, so a page never has to catch anything. */
export type PlaceResult =
  | { readonly status: 'placed'; readonly orderNumber: string; readonly wasRepeat: boolean }
  | { readonly status: 'refused'; readonly reason: string; readonly messages: readonly string[]; readonly shortVariantIds: readonly number[] }
  | { readonly status: 'invalid'; readonly errors: FieldErrors; readonly message: string | null }
  | { readonly status: 'error' };

export type SummaryResult = { readonly status: 'ok'; readonly summary: CheckoutSummary } | { readonly status: 'error' };

/** What the order-placed page needs to show, carried from the checkout page in the navigation state (reload still shows the number). */
export interface PlacedOrder {
  readonly orderNumber: string;
  readonly total: number;
  readonly paymentMethodName: string;
  readonly deliveringTo: string;
  readonly lines: readonly CheckoutLine[];
  readonly subtotal: number;
  readonly discount: number;
  readonly shipping: number | null;
}
