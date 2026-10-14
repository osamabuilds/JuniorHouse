import { LookupDto } from '../models';
import { ReferenceDataApiActions, ReferenceDataPageActions } from './reference-data.actions';
import { referenceDataReducer } from './reference-data.reducer';
import { selectLookup, selectVisibleItems } from './reference-data.selectors';
import { initialReferenceDataState, REFERENCE_DATA_FEATURE_KEY } from './reference-data.state';

const lookup = (id: number, code: string, name: string): LookupDto => ({
  id,
  code,
  name,
  description: null,
  sortSeq: id,
  isActive: true,
  parentCategoryId: null,
  defaultAdvancePercent: null,
});

const root = (state: ReturnType<typeof referenceDataReducer>) => ({ [REFERENCE_DATA_FEATURE_KEY]: state });

describe('referenceDataReducer', () => {
  it('starts loading and clears the search when another type is selected', () => {
    const searched = referenceDataReducer(initialReferenceDataState, ReferenceDataPageActions.searchChanged({ searchText: 'x' }));
    const state = referenceDataReducer(searched, ReferenceDataPageActions.typeSelected({ typeKey: 'colours' }));

    expect(state.selectedTypeKey).toBe('colours');
    expect(state.searchText).toBe('');
    expect(state.loading).toBe(true);
    expect(state.mode).toBe('list');
  });

  it('returns to the list and reloads after a successful save', () => {
    const saving = referenceDataReducer(initialReferenceDataState, ReferenceDataPageActions.createStarted());
    const state = referenceDataReducer(saving, ReferenceDataApiActions.saveSucceeded());

    expect(state.mode).toBe('list');
    expect(state.saving).toBe(false);
    expect(state.loading).toBe(true);
  });

  it('keeps the form open and shows the error when a save fails', () => {
    const error = { status: 400, message: 'No.', fieldErrors: {} };
    const editing = referenceDataReducer(initialReferenceDataState, ReferenceDataPageActions.editStarted({ id: 3 }));
    const state = referenceDataReducer(editing, ReferenceDataApiActions.saveFailed({ error }));

    expect(state.mode).toBe('edit');
    expect(state.error).toEqual(error);
    expect(state.saving).toBe(false);
  });

  it('caches lookup lists separately with and without retired values', () => {
    const active = [lookup(1, 'S', 'Small')];
    const all = [lookup(1, 'S', 'Small'), lookup(2, 'M', 'Medium')];
    let state = referenceDataReducer(initialReferenceDataState, ReferenceDataApiActions.loadLookupsSucceeded({ cacheKey: 'sizes', items: active }));
    state = referenceDataReducer(state, ReferenceDataApiActions.loadLookupsSucceeded({ cacheKey: 'sizes:all', items: all }));

    expect(selectLookup('sizes')(root(state))).toEqual(active);
    expect(selectLookup('sizes', true)(root(state))).toEqual(all);
    expect(selectLookup('cities')(root(state))).toEqual([]);
  });
});

describe('selectVisibleItems', () => {
  it('filters the loaded items by code or name, ignoring case', () => {
    let state = referenceDataReducer(
      initialReferenceDataState,
      ReferenceDataApiActions.loadItemsSucceeded({ items: [lookup(1, 'RED', 'Crimson'), lookup(2, 'BLU', 'Navy')] }),
    );
    state = referenceDataReducer(state, ReferenceDataPageActions.searchChanged({ searchText: ' navy ' }));

    expect(selectVisibleItems(root(state)).map((item) => item.code)).toEqual(['BLU']);
  });
});
