import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CustomerAuthActions, customerAuthFeature } from '../../../../core/customer-auth';
import { ChangeResult, IdsResult, ListResult, WishlistItem, WishlistPage } from '../models/wishlist.model';
import { GuestWishlistStorage } from '../services/guest-wishlist.storage';
import { WishlistApi } from '../services/wishlist-api.service';
import { WishlistActions } from './wishlist.actions';
import { WishlistEffects } from './wishlist.effects';
import { wishlistFeature } from './wishlist.reducer';
import { selectSavedIds, selectWishlistCount } from './wishlist.selectors';

const item = (productId: number): WishlistItem => ({
  productId, name: `Product ${productId}`, path: `/girls/dresses/product-${productId}`, price: 1500, wasPrice: null, savedPrice: 1500, isAvailable: true,
  imageUrl: null, imageFallbackUrl: null, imageWidth: null, notes: [], savedAt: '2026-11-04T09:00:00Z',
});
const page = (items: WishlistItem[], no = 1, totalPages = 1): WishlistPage => ({ items, page: no, pageSize: 24, totalCount: items.length, totalPages });

describe('wishlist store', () => {
  const ids = vi.fn<() => IdsResult>();
  const add = vi.fn<(id: number) => ChangeResult>();
  const remove = vi.fn<(id: number) => ChangeResult>();
  const merge = vi.fn<(ids: readonly number[]) => ChangeResult>();
  const list = vi.fn<(page: number) => ListResult>();
  let remembered: number[];
  const guest = { read: () => [...remembered], write: vi.fn((v: readonly number[]) => (remembered = [...v])), clear: vi.fn(() => (remembered = [])) };

  beforeEach(() => {
    remembered = [];
    ids.mockReset().mockReturnValue({ status: 'ok', ids: [2, 1] });
    add.mockReset().mockReturnValue({ status: 'ok' });
    remove.mockReset().mockReturnValue({ status: 'ok' });
    merge.mockReset().mockReturnValue({ status: 'ok' });
    list.mockReset().mockReturnValue({ status: 'ok', page: page([item(2), item(1)]) });
    guest.write.mockClear();
    guest.clear.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideStore({ [customerAuthFeature.name]: customerAuthFeature.reducer, [wishlistFeature.name]: wishlistFeature.reducer }),
        provideEffects(WishlistEffects),
        { provide: GuestWishlistStorage, useValue: guest },
        {
          provide: WishlistApi,
          useValue: {
            ids: () => of(ids()),
            add: (id: number) => of(add(id)),
            remove: (id: number) => of(remove(id)),
            merge: (v: readonly number[]) => of(merge(v)),
            list: (p: number) => of(list(p)),
          },
        },
      ],
    });
  });

  const store = () => TestBed.inject(Store);
  const state = () => store().selectSignal(wishlistFeature.selectWishlistState)();
  const signIn = () => store().dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'ok', session: { firstName: 'Dev', emailVerified: true, csrfToken: 'x' } } }));
  const signOut = () => store().dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'signedOut' } }));

  it('WSL-1 a signed-in customer has their saved ids read once the session is known', () => {
    signIn();

    expect(state().signedIn).toBe(true);
    expect(state().ids).toEqual([2, 1]);
    expect(state().idsKnown).toBe(true);
    expect(store().selectSignal(selectWishlistCount)()).toBe(2);
  });

  it('WSL-1 pressing the heart saves the product at once and asks the server; pressing again removes it', () => {
    signIn();

    store().dispatch(WishlistActions.toggle({ productId: 5 }));
    expect(state().ids).toEqual([5, 2, 1]);
    expect(add).toHaveBeenCalledWith(5);

    store().dispatch(WishlistActions.toggle({ productId: 5 }));
    expect(state().ids).toEqual([2, 1]);
    expect(remove).toHaveBeenCalledWith(5);
  });

  it('WSL-5 Remove takes a product off the list even if the ids were stale, and never adds', () => {
    signIn();

    store().dispatch(WishlistActions.remove({ productId: 9 }));

    expect(remove).toHaveBeenCalledWith(9);
    expect(add).not.toHaveBeenCalled();
    expect(state().ids).toEqual([2, 1]);
  });

  it('a change the server refused reads the ids again, so the heart shows what the server holds', () => {
    signIn();
    add.mockReturnValue({ status: 'refused', message: 'Your wishlist holds up to 200 products.' });
    ids.mockReturnValue({ status: 'ok', ids: [2, 1] });

    store().dispatch(WishlistActions.toggle({ productId: 5 }));

    expect(state().ids).toEqual([2, 1]);
    expect(state().message).toBe('Your wishlist holds up to 200 products.');
  });

  it('WSL-2 a guest pressing the heart is remembered in the browser and asked to sign in', () => {
    signOut();

    store().dispatch(WishlistActions.toggle({ productId: 7 }));

    expect(state().promptOpen).toBe(true);
    expect(state().guestIds).toEqual([7]);
    expect(guest.write).toHaveBeenCalledWith([7]);
    expect(add).not.toHaveBeenCalled();
    expect(store().selectSignal(selectSavedIds)()).toEqual([7]);

    store().dispatch(WishlistActions.closePrompt());
    expect(state().promptOpen).toBe(false);
  });

  it('WSL-2 a guest pressing a saved heart takes it off without a prompt', () => {
    remembered = [7];
    signOut();

    store().dispatch(WishlistActions.toggle({ productId: 7 }));

    expect(state().guestIds).toEqual([]);
    expect(state().promptOpen).toBe(false);
    expect(guest.write).toHaveBeenLastCalledWith([]);
  });

  it('WSL-3 signing in merges the remembered ids once, clears the browser copy and reads the list', () => {
    remembered = [7, 8];
    signIn();

    expect(merge).toHaveBeenCalledWith([7, 8]);
    expect(guest.clear).toHaveBeenCalledTimes(1);
    expect(state().ids).toEqual([2, 1]);
  });

  it('WSL-3 a merge that failed keeps the browser copy for the next sign-in', () => {
    remembered = [7];
    merge.mockReturnValue({ status: 'error' });
    signIn();

    expect(guest.clear).not.toHaveBeenCalled();
    expect(remembered).toEqual([7]);
  });

  it('WSL-5 loading the page keeps the page and goes back one step when the last item of a page is removed', () => {
    signIn();
    list.mockReturnValueOnce({ status: 'ok', page: page([], 2, 1) }).mockReturnValue({ status: 'ok', page: page([item(1)], 1, 1) });

    store().dispatch(WishlistActions.loadPage({ page: 2 }));

    expect(state().status).toBe('ready');
    expect(state().pageNo).toBe(1);
    expect(state().page?.items.map((i) => i.productId)).toEqual([1]);
  });

  it('WSL-7 a 401 while loading the page means signed out, not an error', () => {
    signIn();
    list.mockReturnValue({ status: 'signedOut' });

    store().dispatch(WishlistActions.loadPage({ page: 1 }));

    expect(state().status).toBe('signedOut');
    expect(state().page).toBeNull();
  });

  it('signing out forgets the account list and reads what this browser remembers', () => {
    signIn();
    remembered = [9];

    signOut();

    expect(state().signedIn).toBe(false);
    expect(state().ids).toEqual([]);
    expect(state().guestIds).toEqual([9]);
  });
});
