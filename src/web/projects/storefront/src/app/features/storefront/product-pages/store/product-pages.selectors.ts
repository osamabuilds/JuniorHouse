import { productPagesFeature } from './product-pages.reducer';

export const {
  selectKey: selectProductPageKey,
  selectResult: selectProductPageResult,
  selectLoading: selectProductPageLoading,
} = productPagesFeature;
