import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { exhaustMap, map, switchMap, take, takeUntil, takeWhile, timer } from 'rxjs';
import { PaymentApi } from '../services/payment-api.service';
import { PaymentActions } from './payment.actions';

/** How often the return page asks, and how many times before it says it has not heard back (about two minutes). */
export const POLL_EVERY_MS = 3000;
export const MAX_POLLS = 40;

@Injectable()
export class PaymentEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(PaymentApi);

  /** exhaustMap: while one start is on its way another press is ignored, so a double click cannot open two pages (no double charge). */
  readonly start$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PaymentActions.startRequested),
      exhaustMap(({ token }) => this.api.start(token).pipe(map((result) => PaymentActions.startDone({ result })))),
    ),
  );

  /**
   * Asks straight away, then every few seconds while the payment is still confirming, at most MAX_POLLS times. switchMap: a new request
   * replaces a running poll; Reset (leaving the page) ends it. Any answer but "confirming" stops the polling by itself.
   */
  readonly status$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PaymentActions.statusRequested),
      switchMap(({ token }) =>
        timer(0, POLL_EVERY_MS).pipe(
          take(MAX_POLLS),
          exhaustMap(() => this.api.status(token)),
          map((result) => PaymentActions.statusLoaded({ result })),
          takeWhile((a) => a.result.status === 'ok' && a.result.payment.state === 'confirming', true),
          takeUntil(this.actions$.pipe(ofType(PaymentActions.reset))),
        ),
      ),
    ),
  );
}
