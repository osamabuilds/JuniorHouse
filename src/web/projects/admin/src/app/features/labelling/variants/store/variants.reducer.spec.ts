import { variantDetail, variantRow, capacity } from '@app/testing/labelling-fixtures';
import { VariantsApiActions, VariantsPageActions } from './variants.actions';
import { variantsReducer } from './variants.reducer';
import { initialVariantsState } from './variants.state';

describe('variantsReducer (spec VAR-8, VAR-12, VAR-13)', () => {
  it('opening the page starts from a clean slate', () => {
    const state = variantsReducer({ ...initialVariantsState, searchText: 'x', status: 'retired', page: 3, notice: 'old' }, VariantsPageActions.opened());

    expect(state).toMatchObject({ searchText: '', status: 'all', page: 1, notice: null, loading: true });
  });

  it('searching or filtering goes back to the first page and reloads', () => {
    const searched = variantsReducer({ ...initialVariantsState, page: 4 }, VariantsPageActions.searchChanged({ searchText: 'ROMP-STY' }));
    expect(searched).toMatchObject({ searchText: 'ROMP-STY', page: 1, loading: true });

    const filtered = variantsReducer({ ...initialVariantsState, page: 4 }, VariantsPageActions.statusChanged({ status: 'retired' }));
    expect(filtered).toMatchObject({ status: 'retired', page: 1, loading: true });
  });

  it('keeps the page of SKUs and the capacity banner data (VAR-12)', () => {
    const loaded = variantsReducer(
      { ...initialVariantsState, loading: true },
      VariantsApiActions.loadVariantsSucceeded({ result: { items: [variantRow()], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }),
    );
    expect(loaded.variants).toHaveLength(1);
    expect(loaded.loading).toBe(false);

    const withCapacity = variantsReducer(loaded, VariantsApiActions.loadCapacitySucceeded({ capacity: capacity({ used: 90000, warning: true, usedPercent: 90 }) }));
    expect(withCapacity.capacity?.warning).toBe(true);
  });

  it('the barcode history opens for one SKU and closes again', () => {
    const asking = variantsReducer(initialVariantsState, VariantsPageActions.detailRequested({ id: 31 }));
    expect(asking.detailLoading).toBe(true);
    expect(asking.detail).toBeNull();

    const loaded = variantsReducer(asking, VariantsApiActions.loadDetailSucceeded({ detail: variantDetail() }));
    expect(loaded.detail?.gtins).toHaveLength(2);
    expect(loaded.detailLoading).toBe(false);

    expect(variantsReducer(loaded, VariantsPageActions.detailClosed()).detail).toBeNull();
  });

  it('voiding: the form opens for one SKU, shows saving, and closes with a notice that the number is gone for good (VAR-8)', () => {
    const variant = variantRow();
    const opened = variantsReducer(initialVariantsState, VariantsPageActions.voidFormOpened({ variant }));
    expect(opened.voidTarget?.id).toBe(31);

    const saving = variantsReducer(opened, VariantsPageActions.voidSubmitted({ id: 31, value: { reasonId: 1, note: null, actingPerson: 'Asha' } }));
    expect(saving.voidSaving).toBe(true);

    const done = variantsReducer(saving, VariantsApiActions.voidSucceeded({ detail: variantDetail() }));
    expect(done.voidTarget).toBeNull();
    expect(done.voidSaving).toBe(false);
    expect(done.notice).toContain('will never be used again');
    expect(done.loading).toBe(true);
  });

  it('a refused void keeps the form open with the reason', () => {
    const saving = variantsReducer(variantsReducer(initialVariantsState, VariantsPageActions.voidFormOpened({ variant: variantRow() })), VariantsPageActions.voidSubmitted({ id: 31, value: { reasonId: 99, note: null, actingPerson: 'Asha' } }));

    const failed = variantsReducer(saving, VariantsApiActions.voidFailed({ error: { status: 400, message: 'Choose why the GTIN is being voided.', fieldErrors: { ReasonId: ['Choose why the GTIN is being voided.'] } } }));

    expect(failed.voidTarget).not.toBeNull();
    expect(failed.voidSaving).toBe(false);
    expect(failed.voidError?.fieldErrors['ReasonId']).toHaveLength(1);
  });
});
