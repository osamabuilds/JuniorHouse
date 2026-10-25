import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { map, switchMap } from 'rxjs';
import { ProductPagesApi } from '../services/product-pages-api.service';
import { ProductPagesActions } from './product-pages.actions';

@Injectable()
export class ProductPagesEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(ProductPagesApi);

  /** switchMap: a newer choice cancels the request for an older one. The browser reuses the server response through the HTTP transfer cache. */
  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(ProductPagesActions.load),
      switchMap(({ key, path, selection }) => this.api.get(path, selection).pipe(map((result) => ProductPagesActions.loadDone({ key, result })))),
    ),
  );
}
