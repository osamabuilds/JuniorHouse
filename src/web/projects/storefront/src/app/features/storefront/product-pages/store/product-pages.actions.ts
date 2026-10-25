import { createActionGroup, props } from '@ngrx/store';
import { ProductPageResult } from '../models/product-page.model';
import { ProductSelection } from '../models/product-selection';

export const ProductPagesActions = createActionGroup({
  source: 'Product Pages',
  events: {
    Load: props<{ key: string; path: string; selection: ProductSelection }>(),
    'Load Done': props<{ key: string; result: ProductPageResult }>(),
  },
});
