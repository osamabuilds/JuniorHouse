import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { concatMap, map, switchMap } from 'rxjs';
import { NewsletterApi } from '../services/newsletter-api.service';
import { NewsletterActions } from './newsletter.actions';

@Injectable()
export class NewsletterEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(NewsletterApi);

  /** switchMap: a second press while one is on its way replaces it. */
  readonly subscribe$ = createEffect(() =>
    this.actions$.pipe(
      ofType(NewsletterActions.subscribe),
      switchMap(({ email }) => this.api.subscribe(email).pipe(map((result) => NewsletterActions.subscribeDone({ result })))),
    ),
  );

  readonly loadMine$ = createEffect(() =>
    this.actions$.pipe(
      ofType(NewsletterActions.loadMine),
      switchMap(() => this.api.mine().pipe(map((result) => NewsletterActions.loadMineDone({ result })))),
    ),
  );

  /** concatMap: switching on then off runs in the order the customer pressed. */
  readonly setMine$ = createEffect(() =>
    this.actions$.pipe(
      ofType(NewsletterActions.setMine),
      concatMap(({ subscribed }) => this.api.setMine(subscribed).pipe(map((result) => NewsletterActions.setMineDone({ result })))),
    ),
  );
}
