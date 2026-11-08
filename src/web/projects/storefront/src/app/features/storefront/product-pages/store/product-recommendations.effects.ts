import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { map, switchMap } from 'rxjs';
import { ProductRecommendationsApi } from '../services/product-recommendations-api.service';
import { ProductRecommendationsActions } from './product-recommendations.actions';

@Injectable()
export class ProductRecommendationsEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(ProductRecommendationsApi);

  /** switchMap: moving to another product cancels the request for the one before. The API never errors, so a failure is an empty result. */
  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(ProductRecommendationsActions.load),
      switchMap(({ key }) => this.api.get(key).pipe(map((result) => ProductRecommendationsActions.loadDone({ key, result })))),
    ),
  );
}
