import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { map, mergeMap } from 'rxjs';
import { NotifyMeApi } from '../services/notify-me-api.service';
import { NotifyMeActions } from './notify-me.actions';

@Injectable()
export class NotifyMeEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(NotifyMeApi);

  /** mergeMap: each size has its own form, so a request for one size never cancels another. */
  readonly request$ = createEffect(() =>
    this.actions$.pipe(
      ofType(NotifyMeActions.request),
      mergeMap(({ variantId, email }) => this.api.request(variantId, email).pipe(map((result) => NotifyMeActions.requestDone({ variantId, result })))),
    ),
  );
}
