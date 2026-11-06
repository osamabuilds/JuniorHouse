import { makeStateKey } from '@angular/core';

/**
 * Server-rendered result pages are handed to the browser so the first client render shows the same content (ADR 0028, HYD-*).
 * Only the slices named here are ever sent. They hold public catalogue and content data, read without the visitor's cookies.
 */
export const TRANSFERRED_SLICES = ['listings', 'searchResults', 'productPages', 'page', 'collectionsPage'] as const;

/** Slices that hold personal or session data. They must never be transferred; a test checks the whitelist against this list. */
export const FORBIDDEN_SLICES = ['cart', 'carts', 'customerAuth', 'wishlist', 'addresses', 'checkout', 'orderLookup', 'codConfirm', 'newsletter', 'notifyMe'] as const;

export type SliceMap = Readonly<Record<string, unknown>>;

export const STATE_TRANSFER_KEY = makeStateKey<SliceMap>('romp-state');

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

/** The whitelisted slices of the store, with any in-flight flag cleared, ready for TransferState. */
export function serialiseSlices(state: SliceMap, allowed: readonly string[] = TRANSFERRED_SLICES): SliceMap {
  const out: Record<string, unknown> = {};
  for (const name of allowed) {
    if ((FORBIDDEN_SLICES as readonly string[]).includes(name)) continue;
    const slice = state[name];
    if (!isRecord(slice)) continue;
    out[name] = 'loading' in slice ? { ...slice, loading: false } : slice;
  }
  return out;
}

/** Keeps only whitelisted object slices from whatever arrived in the page; anything else is dropped (HYD-6). */
function sanitise(transferred: unknown): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  if (!isRecord(transferred)) return out;
  for (const name of TRANSFERRED_SLICES) {
    const slice = transferred[name];
    if (isRecord(slice)) out[name] = slice;
  }
  return out;
}

type Action = { type: string };
type Reducer = (state: Record<string, unknown> | undefined, action: Action) => unknown;

/**
 * Feature slices register one by one (init, then update-reducers), so a slice is seeded the first time it appears in the state and is
 * then used up: later resets start empty again.
 */
export function createSeedingMetaReducer(transferred: unknown): (reducer: Reducer) => Reducer {
  const pending = sanitise(transferred);
  return (reducer) => (state, action) => {
    const names = Object.keys(pending);
    if (names.length === 0 || (action.type !== '@ngrx/store/init' && action.type !== '@ngrx/store/update-reducers')) return reducer(state, action);
    const input = { ...(state ?? {}) };
    for (const name of names) if (!(name in input)) input[name] = pending[name];
    const next = reducer(input, action);
    if (isRecord(next)) for (const name of names) if (name in next) delete pending[name];
    return next;
  };
}
