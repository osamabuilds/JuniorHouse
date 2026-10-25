import { Injectable, TransferState, inject, makeStateKey } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { Menu } from '../models/menu.model';
import { NavigationApi } from '../services/navigation-api.service';
import { NavigationActions } from './navigation.actions';

@Injectable()
export class NavigationEffects {
  static readonly TRANSFER_KEY = makeStateKey<Menu>('navigation-menu');

  private readonly actions$ = inject(Actions);
  private readonly api = inject(NavigationApi);
  private readonly transfer = inject(TransferState);

  /** Loaded once per request on the server and handed to the browser through TransferState: no client fetch before first paint. */
  readonly loadMenu$ = createEffect(() =>
    this.actions$.pipe(
      ofType(NavigationActions.loadMenu),
      switchMap(() => {
        const transferred = this.transfer.get(NavigationEffects.TRANSFER_KEY, null);
        if (transferred) return of(NavigationActions.loadMenuSuccess({ menu: transferred }));
        return this.api.getMenu().pipe(
          tap((menu) => this.transfer.set(NavigationEffects.TRANSFER_KEY, menu)),
          map((menu) => NavigationActions.loadMenuSuccess({ menu })),
          catchError((e: unknown) => of(NavigationActions.loadMenuFailure({ error: e instanceof Error ? e.message : 'unknown error' }))),
        );
      }),
    ),
  );
}
