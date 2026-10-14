import { createReducer, on } from '@ngrx/store';
import { ProductEditorActions, ProductsApiActions, ProductsPageActions } from './products.actions';
import { initialProductsState } from './products.state';

export const productsReducer = createReducer(
  initialProductsState,

  // The store outlives the page, so opening a page starts from a clean slate.
  on(ProductsPageActions.opened, () => ({ ...initialProductsState, listLoading: true })),
  on(ProductsPageActions.searchChanged, (state, { search }) => ({ ...state, search, page: 1, listLoading: true, listError: null })),
  on(ProductsPageActions.statusChanged, (state, { statusId }) => ({ ...state, statusId, page: 1, listLoading: true, listError: null })),
  on(ProductsPageActions.pageChanged, (state, { page }) => ({ ...state, page, listLoading: true, listError: null })),
  on(ProductsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, listLoading: true, listError: null })),
  on(ProductsPageActions.refreshRequested, (state) => ({ ...state, listLoading: true, listError: null })),
  on(ProductsPageActions.createOpened, (state) => ({ ...state, createOpen: true, createError: null, notice: null })),
  on(ProductsPageActions.createClosed, (state) => ({ ...state, createOpen: false, creating: false, createError: null })),
  on(ProductsPageActions.createSubmitted, (state) => ({ ...state, creating: true, createError: null })),
  on(ProductsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(ProductEditorActions.opened, () => ({ ...initialProductsState, loading: true })),
  on(
    ProductEditorActions.contentSaved,
    ProductEditorActions.pricesSaved,
    ProductEditorActions.collectionsSaved,
    ProductEditorActions.publishClicked,
    ProductEditorActions.unpublishClicked,
    ProductEditorActions.archiveClicked,
    ProductEditorActions.variantsRefreshClicked,
    ProductEditorActions.imageUploaded,
    ProductEditorActions.altSaved,
    ProductEditorActions.imagesReordered,
    ProductEditorActions.imageDeleted,
    ProductEditorActions.compositionSaved,
    ProductEditorActions.careSaved,
    ProductEditorActions.pathSaved,
    (state) => ({ ...state, saving: true, error: null, notice: null, publishFailures: null }),
  ),
  on(ProductEditorActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(ProductsApiActions.loadListSucceeded, (state, { result }) => ({
    ...state,
    rows: result.items,
    total: result.totalCount,
    page: result.page,
    listLoading: false,
  })),
  on(ProductsApiActions.loadListFailed, (state, { error }) => ({ ...state, listError: error, listLoading: false })),
  on(ProductsApiActions.createSucceeded, (state, { product }) => ({
    ...state,
    createOpen: false,
    creating: false,
    createError: null,
    listLoading: true,
    notice: `Product "${product.name}" was created as a draft.`,
  })),
  on(ProductsApiActions.createFailed, (state, { error }) => ({ ...state, creating: false, createError: error })),

  on(ProductsApiActions.loadProductSucceeded, (state, { product }) => ({ ...state, product, loading: false, saving: false })),
  on(ProductsApiActions.loadProductFailed, (state, { error }) => ({ ...state, error, loading: false, saving: false })),
  on(ProductsApiActions.productChanged, (state, { product, notice }) => ({ ...state, product, saving: false, error: null, notice })),
  on(ProductsApiActions.publishFinished, (state, { result }) => ({
    ...state,
    product: result.product,
    saving: false,
    error: null,
    publishFailures: result.published ? null : result.failures,
    notice: result.published ? `"${result.product.name}" is now published.` : null,
  })),
  on(ProductsApiActions.childChanged, (state, { notice }) => ({ ...state, notice, saving: false, error: null })),
  on(ProductsApiActions.commandFailed, (state, { error }) => ({ ...state, error, saving: false })),
  on(ProductsApiActions.imagesLoaded, (state, { images }) => ({ ...state, images })),
  on(ProductsApiActions.compositionLoaded, (state, { composition }) => ({ ...state, composition })),
  on(ProductsApiActions.chartLoaded, (state, { chart }) => ({ ...state, chart })),
  on(ProductsApiActions.historyLoaded, (state, { history }) => ({ ...state, history })),
  on(ProductsApiActions.suggestionLoaded, (state, { suggestion }) => ({ ...state, suggestion })),
);
