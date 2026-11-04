import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { Observable, concatMap, distinctUntilChanged, filter, map, of, switchMap, tap, withLatestFrom } from 'rxjs';
import { selectAuthStatus } from '../../../../core/customer-auth';
import { PAGE_SIZE } from '../models/wishlist.model';
import { GuestWishlistStorage } from '../services/guest-wishlist.storage';
import { WishlistApi } from '../services/wishlist-api.service';
import { WishlistActions } from './wishlist.actions';
import { wishlistFeature } from './wishlist.reducer';

@Injectable()
export class WishlistEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(WishlistApi);
  private readonly store = inject(Store);
  private readonly guest = inject(GuestWishlistStorage);

  /** The customer-auth store learns who is signed in; the wishlist follows it. */
  readonly session$ = createEffect(() =>
    this.store.select(selectAuthStatus).pipe(
      filter((status) => status === 'signedIn' || status === 'signedOut'),
      map((status) => status === 'signedIn'),
      distinctUntilChanged(),
      map((signedIn) => WishlistActions.sessionKnown({ signedIn })),
    ),
  );

  /**
   * Signed in: whatever this browser remembered is merged into the account once and the browser copy is cleared (WSL-3), then the list of
   * ids is read for the hearts. Signed out: the ids come from the browser.
   */
  readonly known$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.sessionKnown),
      switchMap(({ signedIn }) => {
        if (!signedIn) return of(WishlistActions.guestLoaded({ ids: this.guest.read() }));
        const remembered = this.guest.read();
        const merged$: Observable<unknown> = remembered.length > 0 ? this.api.merge(remembered).pipe(tap((r) => (r.status === 'ok' ? this.guest.clear() : undefined))) : of(null);
        return merged$.pipe(
          switchMap(() => this.api.ids()),
          map((result) => WishlistActions.idsLoaded({ result })),
        );
      }),
    ),
  );

  /** What a signed-out shopper tapped is written to the browser after every change. */
  readonly rememberGuest$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(WishlistActions.toggle, WishlistActions.remove),
        withLatestFrom(this.store.select(wishlistFeature.selectSignedIn), this.store.select(wishlistFeature.selectGuestIds)),
        filter(([, signedIn]) => signedIn === false),
        tap(([, , ids]) => this.guest.write(ids)),
      ),
    { dispatch: false },
  );

  /**
   * concatMap: changes run in the order the shopper made them. The reducer has already flipped the heart, so the request is the opposite of
   * the state now: a product in the ids was just added, one not in them was just removed.
   */
  readonly change$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.toggle),
      withLatestFrom(this.store.select(wishlistFeature.selectSignedIn), this.store.select(wishlistFeature.selectIds)),
      filter(([, signedIn]) => signedIn === true),
      concatMap(([{ productId }, , ids]) =>
        (ids.includes(productId) ? this.api.add(productId) : this.api.remove(productId)).pipe(map((result) => WishlistActions.changeDone({ productId, result }))),
      ),
    ),
  );

  /** The page for a signed-out shopper: one public call for every remembered product. */
  readonly guestItems$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.guestItemsRequested),
      withLatestFrom(this.store.select(wishlistFeature.selectGuestIds)),
      switchMap(([, ids]) => this.api.cards(ids).pipe(map((items) => WishlistActions.guestItemsLoaded({ items })))),
    ),
  );

  readonly remove$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.remove),
      withLatestFrom(this.store.select(wishlistFeature.selectSignedIn)),
      filter(([, signedIn]) => signedIn === true),
      concatMap(([{ productId }]) => this.api.remove(productId).pipe(map((result) => WishlistActions.changeDone({ productId, result })))),
    ),
  );

  /** After any change that did not go through, the ids are read again so the hearts show what the server holds. */
  readonly resync$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.changeDone),
      filter(({ result }) => result.status === 'notFound' || result.status === 'refused' || result.status === 'error'),
      switchMap(() => this.api.ids().pipe(map((result) => WishlistActions.idsLoaded({ result })))),
    ),
  );

  /** switchMap: asking for another page cancels the request for the one before. */
  readonly loadPage$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.loadPage),
      switchMap(({ page }) => this.api.list(page, PAGE_SIZE).pipe(map((result) => WishlistActions.loadPageDone({ result })))),
    ),
  );

  /** A removal on the wishlist page reads the page again, and a page left empty steps back one. */
  readonly reloadPage$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.changeDone),
      filter(({ result }) => result.status === 'ok'),
      withLatestFrom(this.store.select(wishlistFeature.selectStatus), this.store.select(wishlistFeature.selectPageNo)),
      filter(([, status]) => status === 'ready'),
      map(([, , pageNo]) => WishlistActions.loadPage({ page: pageNo })),
    ),
  );

  readonly stepBack$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WishlistActions.loadPageDone),
      filter(({ result }) => result.status === 'ok' && result.page.items.length === 0 && result.page.page > 1),
      map(({ result }) => WishlistActions.loadPage({ page: (result.status === 'ok' ? result.page.page : 2) - 1 })),
    ),
  );
}
