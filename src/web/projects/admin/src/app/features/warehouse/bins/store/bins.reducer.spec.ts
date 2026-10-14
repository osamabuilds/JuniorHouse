import { binRow, pageOf } from '@app/testing/warehouse-fixtures';
import { ApiError } from '@core/http';
import { BinsApiActions, BinsPageActions } from './bins.actions';
import { binsReducer } from './bins.reducer';
import { initialBinsState } from './bins.state';

const refusal: ApiError = { status: 409, message: 'Bin A-03-2 holds stock (4 units) and cannot be deactivated.', fieldErrors: {} };

describe('binsReducer (spec BIN-1, BIN-2, BIN-4, BIN-6)', () => {
  it('opening the page starts from a clean slate', () => {
    const state = binsReducer({ ...initialBinsState, zone: 'A', page: 3, notice: 'old', formOpen: true }, BinsPageActions.opened());

    expect(state).toMatchObject({ zone: '', page: 1, notice: null, formOpen: false, loading: true });
  });

  it('filtering by zone goes back to the first page and reloads', () => {
    const state = binsReducer({ ...initialBinsState, page: 4 }, BinsPageActions.zoneChanged({ zone: 'B' }));

    expect(state).toMatchObject({ zone: 'B', page: 1, loading: true });
  });

  it('keeps the page of bins and the total (BIN-6)', () => {
    const state = binsReducer({ ...initialBinsState, loading: true }, BinsApiActions.loadBinsSucceeded({ result: pageOf([binRow()], { totalCount: 31 }) }));

    expect(state).toMatchObject({ total: 31, loading: false });
    expect(state.bins).toHaveLength(1);
  });

  it('adding a bin closes the form, announces it and reloads the list (BIN-1)', () => {
    const saving = binsReducer({ ...initialBinsState, formOpen: true }, BinsPageActions.createSubmitted({ value: { zone: 'A', aisle: '03', shelf: '2', actingPerson: 'Ayesha Khan' } }));
    expect(saving.saving).toBe(true);

    const done = binsReducer(saving, BinsApiActions.createSucceeded({ bin: binRow() }));
    expect(done).toMatchObject({ formOpen: false, saving: false, loading: true, notice: 'Bin A-03-2 was added.' });
  });

  it('a refused add keeps the form open with the server\'s reason (BIN-2, BIN-3)', () => {
    const error: ApiError = { status: 409, message: 'Bin A-03-2 already exists.', fieldErrors: {} };
    const state = binsReducer({ ...initialBinsState, formOpen: true, saving: true }, BinsApiActions.createFailed({ error }));

    expect(state).toMatchObject({ formOpen: true, saving: false, formError: error });
  });

  it('switching a bin off replaces just that row, and says what happened (BIN-4)', () => {
    const start = { ...initialBinsState, bins: [binRow({ id: 1 }), binRow({ id: 2, binCode: 'A-03-3' })], togglingId: 2 };

    const state = binsReducer(start, BinsApiActions.toggleSucceeded({ bin: binRow({ id: 2, binCode: 'A-03-3', isActive: false }) }));

    expect(state.bins[1].isActive).toBe(false);
    expect(state.bins[0].isActive).toBe(true);
    expect(state).toMatchObject({ togglingId: null, notice: 'Bin A-03-3 was deactivated.' });
  });

  it('a bin that still holds stock cannot be switched off: the refusal is shown and the row is untouched (BIN-4)', () => {
    const start = { ...initialBinsState, bins: [binRow({ unitsOnHand: 4 })], togglingId: 5 };

    const state = binsReducer(start, BinsApiActions.toggleFailed({ error: refusal }));

    expect(state.error).toBe(refusal);
    expect(state.togglingId).toBeNull();
    expect(state.bins[0].isActive).toBe(true);
  });
});
