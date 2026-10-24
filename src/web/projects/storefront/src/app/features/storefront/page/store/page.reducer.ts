import { createFeature, createReducer, on } from '@ngrx/store';
import { PageResult } from '../services/page-api.service';
import { PageActions } from './page.actions';

export interface PageState {
  /** Result per slug, kept for the life of the request/session. */
  readonly results: Readonly<Record<string, PageResult>>;
}

export const pageFeature = createFeature({
  name: 'page',
  reducer: createReducer(
    { results: {} } as PageState,
    on(PageActions.loaded, (s, { slug, result }): PageState => ({ results: { ...s.results, [slug]: result } })),
  ),
});
