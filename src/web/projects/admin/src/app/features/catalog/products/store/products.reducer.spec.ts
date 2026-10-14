import { chart, compositionCare, image, product, productRow } from '@app/testing/catalog-fixtures';
import { pageOf } from '@app/testing/warehouse-fixtures';
import { ApiError } from '@core/http';
import { ProductEditorActions, ProductsApiActions, ProductsPageActions } from './products.actions';
import { productsReducer } from './products.reducer';
import { initialProductsState } from './products.state';

const conflict: ApiError = { status: 409, message: 'Someone else changed this record while you were working. Reload it and try again.', fieldErrors: {} };

describe('productsReducer (spec PRD-1, PRD-4, PRD-5, PRD-11)', () => {
  it('opening the list starts from a clean slate', () => {
    const state = productsReducer({ ...initialProductsState, search: 'x', page: 4, notice: 'old' }, ProductsPageActions.opened());

    expect(state).toMatchObject({ search: '', page: 1, notice: null, listLoading: true });
  });

  it('a new filter goes back to the first page and reloads (PRD-11)', () => {
    const state = productsReducer({ ...initialProductsState, page: 3 }, ProductsPageActions.statusChanged({ statusId: 2 }));

    expect(state).toMatchObject({ statusId: 2, page: 1, listLoading: true });
  });

  it('keeps the page of products and the total (PRD-11)', () => {
    const state = productsReducer(initialProductsState, ProductsApiActions.loadListSucceeded({ result: pageOf([productRow()], { totalCount: 41 }) }));

    expect(state).toMatchObject({ total: 41, listLoading: false });
    expect(state.rows).toHaveLength(1);
  });

  it('creating a product closes the form, announces it and reloads the list (PRD-1)', () => {
    const open = productsReducer(initialProductsState, ProductsPageActions.createOpened());
    const saving = productsReducer(open, ProductsPageActions.createSubmitted({ value: { styleId: 3, name: 'x', description: '', genderId: 2, categoryId: 3, ageBracketId: 4, actingPerson: 'Ayesha Khan' } }));
    expect(saving.creating).toBe(true);

    const done = productsReducer(saving, ProductsApiActions.createSucceeded({ product: product() }));

    expect(done).toMatchObject({ createOpen: false, creating: false, listLoading: true });
    expect(done.notice).toContain('Floral Summer Dress');
  });

  it('a refused create keeps the form open with the server reason (one product per style)', () => {
    const error: ApiError = { status: 409, message: 'Style STY-1 already has a product (product 5).', fieldErrors: {} };

    const state = productsReducer({ ...initialProductsState, createOpen: true, creating: true }, ProductsApiActions.createFailed({ error }));

    expect(state).toMatchObject({ createOpen: true, creating: false, createError: error });
  });

  it('opening the editor clears what the last product left behind', () => {
    const state = productsReducer({ ...initialProductsState, product: product(), images: [image()], notice: 'old' }, ProductEditorActions.opened({ id: 9 }));

    expect(state).toMatchObject({ product: null, images: [], notice: null, loading: true });
  });

  it('a saved command shows the returned product and says what happened', () => {
    const saving = productsReducer({ ...initialProductsState, product: product() }, ProductEditorActions.contentSaved({ value: { name: 'x', description: '', genderId: 2, categoryId: 3, ageBracketId: 4, careNote: null }, person: 'Ayesha Khan' }));
    expect(saving.saving).toBe(true);

    const done = productsReducer(saving, ProductsApiActions.productChanged({ product: product({ version: 101 }), notice: 'Content saved.' }));

    expect(done).toMatchObject({ saving: false, notice: 'Content saved.' });
    expect(done.product?.version).toBe(101);
  });

  it('a stale save is shown as a conflict and keeps the product as it was (PRD-5)', () => {
    const state = productsReducer({ ...initialProductsState, product: product(), saving: true }, ProductsApiActions.commandFailed({ error: conflict }));

    expect(state.error).toBe(conflict);
    expect(state.saving).toBe(false);
    expect(state.product?.version).toBe(100);
  });

  it('a refused publish keeps every failure so each can link to its tab (PRD-4)', () => {
    const failures = [
      { code: 'NO_IMAGE', message: 'Add at least one image.', tab: 'images' as const },
      { code: 'CARE_MISSING', message: 'Choose at least one care instruction.', tab: 'composition' as const },
    ];

    const state = productsReducer(
      { ...initialProductsState, saving: true },
      ProductsApiActions.publishFinished({ result: { published: false, product: product(), failures } }),
    );

    expect(state.publishFailures).toEqual(failures);
    expect(state.notice).toBeNull();
  });

  it('a successful publish clears the failures and announces it (PRD-10)', () => {
    const state = productsReducer(
      { ...initialProductsState, publishFailures: [{ code: 'NO_IMAGE', message: 'x', tab: 'images' }] },
      ProductsApiActions.publishFinished({ result: { published: true, product: product({ status: 'Published' }), failures: [] } }),
    );

    expect(state.publishFailures).toBeNull();
    expect(state.notice).toContain('published');
    expect(state.product?.status).toBe('Published');
  });

  it('the next action clears the old failures and notice', () => {
    const state = productsReducer(
      { ...initialProductsState, publishFailures: [{ code: 'NO_IMAGE', message: 'x', tab: 'images' }], notice: 'old' },
      ProductEditorActions.publishClicked({ person: 'Ayesha Khan' }),
    );

    expect(state).toMatchObject({ publishFailures: null, notice: null, saving: true });
  });

  it('keeps the data of the other tabs as they arrive', () => {
    let state = productsReducer(initialProductsState, ProductsApiActions.imagesLoaded({ images: [image()] }));
    state = productsReducer(state, ProductsApiActions.compositionLoaded({ composition: compositionCare() }));
    state = productsReducer(state, ProductsApiActions.chartLoaded({ chart: chart() }));
    state = productsReducer(state, ProductsApiActions.suggestionLoaded({ suggestion: { slug: 'floral-2', path: '/girls/dresses/floral-2' } }));

    expect(state.images).toHaveLength(1);
    expect(state.composition?.isComplete).toBe(true);
    expect(state.chart?.found).toBe(true);
    expect(state.suggestion?.slug).toBe('floral-2');
  });
});
