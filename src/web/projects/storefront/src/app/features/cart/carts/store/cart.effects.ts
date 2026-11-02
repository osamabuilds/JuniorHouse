import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { concatMap, map, switchMap } from 'rxjs';
import { CartApi } from '../services/cart-api.service';
import { CartActions } from './cart.actions';

@Injectable()
export class CartEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(CartApi);

  /** switchMap: a newer load replaces one still on its way. */
  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CartActions.load),
      switchMap(() => this.api.load().pipe(map((result) => CartActions.loadDone({ result })))),
    ),
  );

  /** concatMap: changes run in the order the shopper made them, one at a time. */
  readonly add$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CartActions.add),
      concatMap(({ variantId, quantity }) => this.api.add(variantId, quantity).pipe(map((result) => CartActions.changeDone({ kind: 'add', result })))),
    ),
  );

  readonly setQuantity$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CartActions.setQuantity),
      concatMap(({ variantId, quantity }) => this.api.setQuantity(variantId, quantity).pipe(map((result) => CartActions.changeDone({ kind: 'set', result })))),
    ),
  );

  readonly remove$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CartActions.remove),
      concatMap(({ variantId }) => this.api.remove(variantId).pipe(map((result) => CartActions.changeDone({ kind: 'remove', result })))),
    ),
  );
}
