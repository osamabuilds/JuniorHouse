import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { exhaustMap, map, switchMap } from 'rxjs';
import { CheckoutApi } from '../services/checkout-api.service';
import { CheckoutActions } from './checkout.actions';

@Injectable()
export class CheckoutEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(CheckoutApi);

  /** The page opens: the provinces for the address, and the summary with no city yet. */
  readonly opened$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CheckoutActions.opened),
      switchMap(() => this.api.provinces().pipe(map((provinces) => CheckoutActions.provincesLoaded({ provinces })))),
    ),
  );

  readonly openedSummary$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CheckoutActions.opened),
      map(() => CheckoutActions.summaryRequested({ cityId: null, method: 'COD' })),
    ),
  );

  /** switchMap: a newer city or method replaces a request still on its way. */
  readonly summary$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CheckoutActions.summaryRequested),
      switchMap(({ cityId, method }) => this.api.summary(cityId, method).pipe(map((result) => CheckoutActions.summaryLoaded({ result })))),
    ),
  );

  readonly cities$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CheckoutActions.provinceChosen),
      switchMap(({ provinceId }) => this.api.cities(provinceId).pipe(map((cities) => CheckoutActions.citiesLoaded({ cities })))),
    ),
  );

  /** exhaustMap: while one send is in flight, another press is ignored (CHK-10). */
  readonly submit$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CheckoutActions.submitted),
      exhaustMap(({ form, key }) => this.api.place(form, key).pipe(map((result) => CheckoutActions.submitDone({ result })))),
    ),
  );
}
