import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { exhaustMap, map, switchMap } from 'rxjs';
import { OrderLookupApi } from '../services/order-lookup-api.service';
import { OrderLookupActions } from './order-lookup.actions';

@Injectable()
export class OrderLookupEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(OrderLookupApi);

  /** exhaustMap: while one attempt is on its way another press is ignored, so a double tap is not two failures. */
  readonly lookup$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderLookupActions.lookupSubmitted),
      exhaustMap(({ input }) => this.api.lookup(input).pipe(map((result) => OrderLookupActions.lookupDone({ result })))),
    ),
  );

  readonly link$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderLookupActions.linkOpened),
      switchMap(({ token }) => this.api.byLink(token).pipe(map((result) => OrderLookupActions.linkDone({ result })))),
    ),
  );
}
