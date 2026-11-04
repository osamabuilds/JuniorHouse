/** The answers the footer form can get. `invalid` is an address that cannot be read; `busy` is the rate limit; everything else that fails is `error`. */
export type SubscribeResult = 'ok' | 'invalid' | 'busy' | 'error';

export type MineResult =
  | { readonly status: 'ok'; readonly subscribed: boolean }
  | { readonly status: 'signedOut' }
  | { readonly status: 'error' };

/** What the result page tells the visitor, chosen by the path and the `status` the server put in the address. */
export type ResultKind = 'subscribed' | 'unsubscribed';
export type ResultStatus = 'ok' | 'invalid' | 'error';
