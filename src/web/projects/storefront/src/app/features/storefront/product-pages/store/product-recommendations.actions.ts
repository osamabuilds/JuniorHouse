import { createActionGroup, props } from '@ngrx/store';
import { ProductRecommendations } from '../models/product-recommendations.model';

export const ProductRecommendationsActions = createActionGroup({
  source: 'Product Recommendations',
  events: {
    Load: props<{ key: string }>(),
    'Load Done': props<{ key: string; result: ProductRecommendations }>(),
  },
});
