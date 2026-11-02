import { createReducer, on } from '@ngrx/store';
import { DiscountCodesApiActions, DiscountCodesPageActions } from './discount-codes.actions';
import { initialDiscountCodesState } from './discount-codes.state';

export const discountCodesReducer = createReducer(
  initialDiscountCodesState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(DiscountCodesPageActions.opened, () => ({ ...initialDiscountCodesState, loading: true })),
  on(DiscountCodesPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(DiscountCodesPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(DiscountCodesPageActions.editStarted, (state, { id }) => ({ ...state, editingId: id, error: null, notice: null })),
  on(DiscountCodesPageActions.editCancelled, (state) => ({ ...state, editingId: null, error: null })),
  on(DiscountCodesPageActions.submitted, (state) => ({ ...state, saving: true, error: null, notice: null })),
  on(DiscountCodesPageActions.switched, (state, { id }) => ({ ...state, switchingId: id, error: null, notice: null })),
  on(DiscountCodesPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(DiscountCodesApiActions.loadSucceeded, (state, { result }) => ({ ...state, codes: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(DiscountCodesApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(DiscountCodesApiActions.saveSucceeded, (state, { code, created }) => ({
    ...state,
    saving: false,
    editingId: null,
    loading: true,
    notice: created ? `The code ${code.code} was created.` : `The code ${code.code} was saved.`,
  })),
  on(DiscountCodesApiActions.saveFailed, (state, { error }) => ({ ...state, saving: false, error })),
  on(DiscountCodesApiActions.switchSucceeded, (state, { code }) => ({
    ...state,
    switchingId: null,
    codes: state.codes.map((c) => (c.id === code.id ? code : c)),
    notice: code.isActive ? `The code ${code.code} is on.` : `The code ${code.code} is off. Shoppers are told it is not valid.`,
  })),
  on(DiscountCodesApiActions.switchFailed, (state, { error }) => ({ ...state, switchingId: null, error })),
);
