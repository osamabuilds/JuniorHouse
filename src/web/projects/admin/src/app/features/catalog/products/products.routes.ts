import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as productsEffects from './store/products.effects';
import { productsReducer } from './store/products.reducer';
import { PRODUCTS_FEATURE_KEY } from './store/products.state';

/** The list and the editor share one store, so it is registered once on the parent route. */
export const PRODUCTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(PRODUCTS_FEATURE_KEY, productsReducer), provideEffects(productsEffects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/products-page/products-page.container').then((m) => m.ProductsPageContainer),
        title: 'Products · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/product-editor-page/product-editor-page.container').then((m) => m.ProductEditorPageContainer),
        title: 'Product · Romp Admin',
      },
    ],
  },
];
