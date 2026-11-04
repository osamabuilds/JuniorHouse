export interface Address {
  readonly id: number;
  readonly recipientName: string;
  readonly phone: string;
  readonly provinceId: number;
  readonly provinceName: string;
  readonly cityId: number;
  readonly cityName: string;
  readonly lineOne: string;
  readonly lineTwo: string | null;
  readonly postcode: string | null;
  readonly isDefault: boolean;
}

/** What the customer types. The customer is never part of it: the API knows who is signed in (SEC-05). */
export interface AddressInput {
  readonly recipientName: string;
  readonly phone: string;
  readonly provinceId: number;
  readonly cityId: number;
  readonly lineOne: string;
  readonly lineTwo: string | null;
  readonly postcode: string | null;
}

export interface AddressPage {
  readonly items: readonly Address[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
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

/** Field name (as the form uses it) to the message that says what to do. */
export type FieldErrors = Readonly<Record<string, string>>;

/** Every way a call can end, so a page never has to catch anything. */
export type LoadResult = { readonly status: 'ok'; readonly page: AddressPage } | { readonly status: 'signedOut' } | { readonly status: 'error' };

export type ChangeResult =
  | { readonly status: 'ok' }
  | { readonly status: 'invalid'; readonly errors: FieldErrors }
  /** The rule says no (for example the limit); the message is for the customer. */
  | { readonly status: 'refused'; readonly message: string }
  | { readonly status: 'notFound' }
  | { readonly status: 'signedOut' }
  | { readonly status: 'error' };

export const PAGE_SIZE = 10;

export const EMPTY_INPUT: AddressInput = { recipientName: '', phone: '', provinceId: 0, cityId: 0, lineOne: '', lineTwo: null, postcode: null };
