import { createReducer, on } from '@ngrx/store';
import { VariantsApiActions, VariantsPageActions } from './variants.actions';
import { initialVariantsState } from './variants.state';

export const variantsReducer = createReducer(
  initialVariantsState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(VariantsPageActions.opened, () => ({ ...initialVariantsState, loading: true })),
  on(VariantsPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText, page: 1, loading: true, error: null })),
  on(VariantsPageActions.statusChanged, (state, { status }) => ({ ...state, status, page: 1, loading: true, error: null })),
  on(VariantsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(VariantsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(VariantsPageActions.refreshRequested, (state) => ({ ...state, loading: true, error: null })),
  on(VariantsPageActions.detailRequested, (state) => ({ ...state, detail: null, detailLoading: true, error: null })),
  on(VariantsPageActions.detailClosed, (state) => ({ ...state, detail: null, detailLoading: false })),
  on(VariantsPageActions.voidFormOpened, (state, { variant }) => ({ ...state, voidTarget: variant, voidError: null, notice: null })),
  on(VariantsPageActions.voidFormClosed, (state) => ({ ...state, voidTarget: null, voidSaving: false, voidError: null })),
  on(VariantsPageActions.voidSubmitted, (state) => ({ ...state, voidSaving: true, voidError: null })),
  on(VariantsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(VariantsApiActions.loadVariantsSucceeded, (state, { result }) => ({
    ...state,
    variants: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(VariantsApiActions.loadVariantsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(VariantsApiActions.loadCapacitySucceeded, (state, { capacity }) => ({ ...state, capacity })),
  on(VariantsApiActions.loadCapacityFailed, (state, { error }) => ({ ...state, error })),
  on(VariantsApiActions.loadDetailSucceeded, (state, { detail }) => ({ ...state, detail, detailLoading: false })),
  on(VariantsApiActions.loadDetailFailed, (state, { error }) => ({ ...state, error, detailLoading: false })),
  on(VariantsApiActions.voidSucceeded, (state, { detail }) => ({
    ...state,
    voidTarget: null,
    voidSaving: false,
    voidError: null,
    detail: state.detail ? detail : null,
    loading: true,
    notice: `The barcode for ${detail.variant.skuCode} was voided. That number will never be used again.`,
  })),
  on(VariantsApiActions.voidFailed, (state, { error }) => ({ ...state, voidSaving: false, voidError: error })),
);
