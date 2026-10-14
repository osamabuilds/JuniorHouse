import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as variantsEffects from './store/variants.effects';
import { variantsReducer } from './store/variants.reducer';
import { VARIANTS_FEATURE_KEY } from './store/variants.state';

export const VARIANTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(VARIANTS_FEATURE_KEY, variantsReducer), provideEffects(variantsEffects)],
    loadComponent: () => import('./containers/sku-register-page/sku-register-page.container').then((m) => m.SkuRegisterPageContainer),
    title: 'SKU register · Romp Admin',
  },
];
