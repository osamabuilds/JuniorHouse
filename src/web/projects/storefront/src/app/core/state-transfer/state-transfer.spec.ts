import { describe, expect, it } from 'vitest';
import { FORBIDDEN_SLICES, TRANSFERRED_SLICES, createSeedingMetaReducer, serialiseSlices } from './state-transfer';

const reducerOf = (keys: readonly string[]) => (state: Record<string, unknown> | undefined, action: { type: string }) => {
  void action;
  const out: Record<string, unknown> = {};
  for (const k of keys) out[k] = state?.[k] ?? { key: null, result: null, loading: false };
  return out;
};

describe('state transfer', () => {
  it('HYD-3 the whitelist never names a customer, cart, wishlist, address, checkout or order slice', () => {
    for (const forbidden of FORBIDDEN_SLICES) expect(TRANSFERRED_SLICES as readonly string[]).not.toContain(forbidden);
    expect([...TRANSFERRED_SLICES].sort()).toEqual(['collectionsPage', 'listings', 'page', 'productPages', 'searchResults']);
  });

  it('HYD-3 HYD-4 serialise keeps only whitelisted slices and clears loading', () => {
    const out = serialiseSlices({
      listings: { key: 'k', result: { a: 1 }, loading: true },
      cart: { items: [1] },
      customerAuth: { token: 'x' },
      wishlist: { ids: [1] },
    });
    expect(Object.keys(out)).toEqual(['listings']);
    expect(out['listings']).toEqual({ key: 'k', result: { a: 1 }, loading: false });
  });

  it('HYD-3 serialise ignores a forbidden slice even if it was added to the whitelist by mistake', () => {
    const out = serialiseSlices({ cart: { items: [1] } }, ['cart']);
    expect(out).toEqual({});
  });

  it('HYD-1 the meta-reducer seeds a slice the first time its reducer is registered, then stops', () => {
    const seed = { listings: { key: 'k', result: { a: 1 }, loading: false } };
    const meta = createSeedingMetaReducer(seed);
    // INIT: only the root is known; the slice reducer arrives later with an update action.
    const afterInit = meta(reducerOf([]))(undefined, { type: '@ngrx/store/init' });
    expect(afterInit).toEqual({});
    const afterUpdate = meta(reducerOf(['listings']))(afterInit as Record<string, unknown>, { type: '@ngrx/store/update-reducers' }) as Record<string, unknown>;
    expect(afterUpdate['listings']).toEqual(seed.listings);
  });

  it('HYD-1 HYD-6 a seeded slice is used once: after a reset the slice starts empty again', () => {
    const meta = createSeedingMetaReducer({ listings: { key: 'k', result: { a: 1 }, loading: false } });
    const reducer = meta(reducerOf(['listings']));
    reducer(undefined, { type: '@ngrx/store/init' });
    const again = reducer(undefined, { type: '@ngrx/store/update-reducers' }) as Record<string, unknown>;
    expect(again['listings']).toEqual({ key: null, result: null, loading: false });
  });

  it('HYD-6 malformed or missing transferred data seeds nothing', () => {
    for (const bad of [null, undefined, 'x', 5, [], { listings: 'nope' }, { listings: null }]) {
      const meta = createSeedingMetaReducer(bad as unknown);
      const out = meta(reducerOf(['listings']))(undefined, { type: '@ngrx/store/init' }) as Record<string, unknown>;
      expect(out['listings']).toEqual({ key: null, result: null, loading: false });
    }
  });

  it('HYD-3 the meta-reducer ignores a slice that is not on the whitelist', () => {
    const meta = createSeedingMetaReducer({ cart: { items: [1] } });
    const out = meta(reducerOf(['cart']))(undefined, { type: '@ngrx/store/init' }) as Record<string, unknown>;
    expect(out['cart']).toEqual({ key: null, result: null, loading: false });
  });
});
