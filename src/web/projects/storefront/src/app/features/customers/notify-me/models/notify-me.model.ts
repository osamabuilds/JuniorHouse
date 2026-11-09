/** What a back-in-stock request can come back with. `invalid` carries the server's own words (a bad address, a size that is in stock); `busy` is the rate limit. */
export type NotifyResult =
  | { readonly status: 'ok' }
  | { readonly status: 'invalid'; readonly message: string }
  | { readonly status: 'busy' }
  | { readonly status: 'error' };

/** The state of the form for one size (variant). Absent means untouched. */
export type NotifyForm =
  | { readonly status: 'sending' }
  | { readonly status: 'done' }
  | { readonly status: 'invalid'; readonly message: string }
  | { readonly status: 'busy' }
  | { readonly status: 'error' };

/** What the result page tells the visitor after the form without JavaScript; chosen by the `status` the server put in the address. */
export type NotifyResultStatus = 'ok' | 'invalid' | 'error';
