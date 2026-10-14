import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, merge, mergeMap, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { ProductApiService } from '../services/product-api.service';
import { ProductEditorActions, ProductsApiActions, ProductsPageActions } from './products.actions';
import { selectFilters, selectProduct } from './products.selectors';

const fail = (error: ApiError) => of(ProductsApiActions.commandFailed({ error }));

/** Re-runs the list whenever a filter changes or a create changed what the list shows. */
export const loadList$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(
        ProductsPageActions.opened,
        ProductsPageActions.searchChanged,
        ProductsPageActions.statusChanged,
        ProductsPageActions.pageChanged,
        ProductsPageActions.pageSizeChanged,
        ProductsPageActions.refreshRequested,
        ProductsApiActions.createSucceeded,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.list(filters).pipe(
          map((result) => ProductsApiActions.loadListSucceeded({ result })),
          catchError((error: ApiError) => of(ProductsApiActions.loadListFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const createProduct$ = createEffect(
  (actions$ = inject(Actions), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductsPageActions.createSubmitted),
      switchMap(({ value }) =>
        api.create(value).pipe(
          map((product) => ProductsApiActions.createSucceeded({ product })),
          catchError((error: ApiError) => of(ProductsApiActions.createFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Opening the editor reads the product and the data of every tab, so each tab is ready when the person gets to it. */
export const openEditor$ = createEffect(
  (actions$ = inject(Actions), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.opened),
      switchMap(({ id }) =>
        api.get(id).pipe(
          mergeMap((product) => [ProductsApiActions.loadProductSucceeded({ product })]),
          catchError((error: ApiError) => of(ProductsApiActions.loadProductFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Whenever the product is read (or changed), the tabs that depend on it are read again: images, composition, chart, URL history. */
export const loadTabs$ = createEffect(
  (actions$ = inject(Actions), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductsApiActions.loadProductSucceeded),
      switchMap(({ product }) =>
        merge(
          api.images(product.id).pipe(map((images) => ProductsApiActions.imagesLoaded({ images }))),
          api.compositionCare(product.id).pipe(map((composition) => ProductsApiActions.compositionLoaded({ composition }))),
          api.sizeChart(product.id).pipe(map((chart) => ProductsApiActions.chartLoaded({ chart }))),
          api.pathHistory(product.id).pipe(map((history) => ProductsApiActions.historyLoaded({ history }))),
        ).pipe(catchError((error: ApiError) => of(ProductsApiActions.commandFailed({ error })))),
      ),
    ),
  { functional: true },
);


export const saveContent$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.contentSaved),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ value, person }, product]) =>
        api.saveContent(product!.id, value, product!.version, person).pipe(
          map((changed) => ProductsApiActions.productChanged({ product: changed, notice: 'Content saved.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const savePrices$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.pricesSaved),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ prices, person }, product]) =>
        api.savePrices(product!.id, prices, product!.version, person).pipe(
          map((changed) => ProductsApiActions.productChanged({ product: changed, notice: 'Prices saved.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const saveCollections$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.collectionsSaved),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ collectionIds, person }, product]) =>
        api.saveCollections(product!.id, collectionIds, product!.version, person).pipe(
          map((changed) => ProductsApiActions.productChanged({ product: changed, notice: 'Collections saved.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const publish$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.publishClicked),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ person }, product]) =>
        api.publish(product!.id, product!.version, person).pipe(
          map((result) => ProductsApiActions.publishFinished({ result })),
          // A refused publish is a 422 carrying every failure (PRD-4): the editor lists them rather than showing a single error.
          catchError((error: ApiError) => (error.status === 422 ? reread(api, product!.id) : fail(error))),
        ),
      ),
    ),
  { functional: true },
);

function reread(api: ProductApiService, id: number) {
  return api.get(id).pipe(
    map((product) =>
      ProductsApiActions.publishFinished({
        result: { published: false, product, failures: product.gate.failures },
      }),
    ),
    catchError(fail),
  );
}

export const unpublish$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.unpublishClicked),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ person }, product]) =>
        api.unpublish(product!.id, product!.version, person).pipe(
          map((changed) => ProductsApiActions.productChanged({ product: changed, notice: 'The product is off sale. Nothing was deleted.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const archive$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.archiveClicked),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ person }, product]) =>
        api.archive(product!.id, product!.version, person).pipe(
          map((changed) => ProductsApiActions.productChanged({ product: changed, notice: 'The product was archived.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const refreshVariants$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.variantsRefreshClicked),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([, product]) =>
        api.refreshVariants(product!.id).pipe(
          map((changed) => ProductsApiActions.productChanged({ product: changed, notice: 'Barcodes were read again from Labelling.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const uploadImage$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.imageUploaded),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ value, person }, product]) =>
        api.uploadImage(product!.id, value, person).pipe(
          map(() => ProductsApiActions.childChanged({ notice: 'Image added.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const saveAlt$ = createEffect(
  (actions$ = inject(Actions), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.altSaved),
      switchMap(({ imageId, text, person }) =>
        api.saveAlt(imageId, text, person).pipe(
          map(() => ProductsApiActions.childChanged({ notice: 'Alt text saved.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const reorderImages$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.imagesReordered),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ imageIds, person }, product]) =>
        api.reorderImages(product!.id, imageIds, person).pipe(
          map(() => ProductsApiActions.childChanged({ notice: 'Image order saved.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const deleteImage$ = createEffect(
  (actions$ = inject(Actions), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.imageDeleted),
      switchMap(({ imageId, person }) =>
        api.deleteImage(imageId, person).pipe(
          map(() => ProductsApiActions.childChanged({ notice: 'Image deleted.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const saveComposition$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.compositionSaved),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ rows, person }, product]) =>
        api.saveComposition(product!.id, rows, product!.version, person).pipe(
          map(() => ProductsApiActions.childChanged({ notice: 'Composition saved.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const saveCare$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.careSaved),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ careIds, note, person }, product]) =>
        api.saveCare(product!.id, careIds, note, product!.version, person).pipe(
          map(() => ProductsApiActions.childChanged({ notice: 'Care instructions saved.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const suggestSlug$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.slugSuggestionRequested),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ name, genderId, categoryId }, product]) =>
        api.suggestSlug(name, genderId, categoryId, product?.id ?? null).pipe(
          map((suggestion) => ProductsApiActions.suggestionLoaded({ suggestion })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const savePath$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductEditorActions.pathSaved),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([{ value, person }, product]) =>
        api.savePath(product!.id, value, product!.version, person).pipe(
          map((saved) => ProductsApiActions.childChanged({ notice: `The address is now ${saved.path}. The old address still works.` })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

/** After a change whose answer is not the product, read the product again so the next edit carries a current version. */
export const reloadAfterChild$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductApiService)) =>
    actions$.pipe(
      ofType(ProductsApiActions.childChanged),
      withLatestFrom(store.select(selectProduct)),
      switchMap(([, product]) =>
        api.get(product!.id).pipe(
          map((fresh) => ProductsApiActions.loadProductSucceeded({ product: fresh })),
          catchError((error: ApiError) => of(ProductsApiActions.loadProductFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** A command that returns the product also changes what the other tabs show (the URL history, the chart, the gate), so they are read again. */
export const reloadTabsAfterProductChanged$ = createEffect(
  (actions$ = inject(Actions)) =>
    actions$.pipe(
      ofType(ProductsApiActions.productChanged, ProductsApiActions.publishFinished),
      map((action) =>
        ProductsApiActions.loadProductSucceeded({ product: 'result' in action ? action.result.product : action.product }),
      ),
    ),
  { functional: true },
);
