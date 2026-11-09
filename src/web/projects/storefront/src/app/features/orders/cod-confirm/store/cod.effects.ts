import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { exhaustMap, map, switchMap } from 'rxjs';
import { CodApi } from '../services/cod-api.service';
import { CodActions } from './cod.actions';

@Injectable()
export class CodEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(CodApi);

  readonly status$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CodActions.statusRequested),
      switchMap(({ token }) => this.api.status(token).pipe(map((result) => CodActions.statusLoaded({ result })))),
    ),
  );

  /** exhaustMap: while one answer is on its way, another press is ignored. */
  readonly answered$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CodActions.answered),
      exhaustMap(({ token, answer }) => this.api.respond(token, answer).pipe(map((result) => CodActions.answerDone({ result })))),
    ),
  );
}
