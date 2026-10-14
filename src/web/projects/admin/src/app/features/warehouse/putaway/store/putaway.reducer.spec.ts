import { pageOf, putawayItem, putawayRecord, scanLookup, selection } from '@app/testing/warehouse-fixtures';
import { ApiError } from '@core/http';
import { PutawayApiActions, PutawayPageActions } from './putaway.actions';
import { putawayReducer, scanMessageFor } from './putaway.reducer';
import { initialPutawayState } from './putaway.state';

const unknownBarcode: ApiError = { status: 404, message: 'Not Found', fieldErrors: {} };

describe('putawayReducer (spec PUT-4, PUT-6, PUT-7, PUT-8, PUT-14, PUT-19)', () => {
  it('opening the page starts from a clean slate', () => {
    const state = putawayReducer({ ...initialPutawayState, notice: 'old', selection: selection(), itemsPage: 4 }, PutawayPageActions.opened());

    expect(state).toMatchObject({ notice: null, selection: null, itemsPage: 1, loading: true, recordsLoading: true });
  });

  it('keeps the queue and the history as loaded (PUT-14)', () => {
    const withItems = putawayReducer(initialPutawayState, PutawayApiActions.loadItemsSucceeded({ result: pageOf([putawayItem()], { totalCount: 25 }) }));
    expect(withItems).toMatchObject({ itemsTotal: 25, loading: false });

    const withRecords = putawayReducer(withItems, PutawayApiActions.loadRecordsSucceeded({ result: pageOf([putawayRecord()]) }));
    expect(withRecords.records).toHaveLength(1);
  });

  it('finding an item by SKU or barcode goes back to the first page of the queue and reloads it (PUT-19)', () => {
    const state = putawayReducer({ ...initialPutawayState, itemsPage: 4 }, PutawayPageActions.itemSearchChanged({ search: 'ROMP-STY' }));

    expect(state).toMatchObject({ itemSearch: 'ROMP-STY', itemsPage: 1, loading: true });
  });

  it('a scan clears the old answer, then keeps the found variant and its pending items (PUT-6)', () => {
    const scanning = putawayReducer({ ...initialPutawayState, scanMessage: 'old' }, PutawayPageActions.scanSubmitted({ gtin: '2000000000015' }));
    expect(scanning).toMatchObject({ scanning: true, scanMessage: null, scanResult: null });

    const found = putawayReducer(scanning, PutawayApiActions.scanSucceeded({ result: scanLookup() }));
    expect(found).toMatchObject({ scanning: false, scanMessage: null });
    expect(found.scanResult?.pendingItems).toHaveLength(1);
  });

  it('an unknown barcode says so and points at the list (PUT-6)', () => {
    const state = putawayReducer({ ...initialPutawayState, scanning: true }, PutawayApiActions.scanFailed({ error: unknownBarcode }));

    expect(state.scanMessage).toContain('not known to the warehouse');
    expect(state.scanMessage).toContain('choose the item from the list');
    expect(scanMessageFor(400, 'That is not a valid GTIN-13 barcode.')).toBe('That is not a valid GTIN-13 barcode.');
  });

  it('a retired barcode, and a barcode with nothing waiting, each get their own message (PUT-7)', () => {
    const retired = putawayReducer(initialPutawayState, PutawayApiActions.scanSucceeded({ result: scanLookup({ gtinRetired: true }) }));
    expect(retired.scanMessage).toContain('retired');

    const nothing = putawayReducer(initialPutawayState, PutawayApiActions.scanSucceeded({ result: scanLookup({ pendingItems: [] }) }));
    expect(nothing.scanMessage).toContain('Nothing is waiting');
  });

  it('choosing an item from the list (no tag to scan) selects it as not scanned (PUT-19)', () => {
    const chosen = selection({ scanned: false, gtin: null });

    const state = putawayReducer(initialPutawayState, PutawayPageActions.itemSelected({ selection: chosen }));

    expect(state.selection).toEqual(chosen);
    expect(state.selection?.scanned).toBe(false);
    expect(putawayReducer(state, PutawayPageActions.selectionCleared()).selection).toBeNull();
  });

  it('a successful confirm clears the selection, reloads, and asks the scan box to take focus for the next scan (PUT-4, PUT-14)', () => {
    const start = { ...initialPutawayState, selection: selection(), saving: true, focusToken: 2 };

    const state = putawayReducer(start, PutawayApiActions.confirmSucceeded({ record: putawayRecord() }));

    expect(state).toMatchObject({ selection: null, saving: false, loading: true, recordsLoading: true, focusToken: 3 });
    expect(state.notice).toContain('4 unit(s) of ROMP-STY-DRESS-RED-2-3Y put away in bin A-03-2');
    expect(state.notice).toContain('6 still to do');
  });

  it('a refused confirm keeps the form and the idempotency key, with the server\'s reason (PUT-5, PUT-6)', () => {
    const error: ApiError = { status: 409, message: 'Bin A-03-2 is inactive.', fieldErrors: {} };
    const start = { ...initialPutawayState, selection: selection(), saving: true };

    const state = putawayReducer(start, PutawayApiActions.confirmFailed({ error }));

    expect(state).toMatchObject({ saving: false, confirmError: error });
    expect(state.selection?.idempotencyKey).toBe('11111111-1111-4111-8111-111111111111');
  });
});
