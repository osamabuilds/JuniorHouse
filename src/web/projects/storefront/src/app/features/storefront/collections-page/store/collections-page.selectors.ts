import { collectionsPageFeature } from './collections-page.reducer';

export const {
  selectKey: selectCollectionsKey,
  selectResult: selectCollectionsResult,
  selectLoading: selectCollectionsLoading,
} = collectionsPageFeature;
