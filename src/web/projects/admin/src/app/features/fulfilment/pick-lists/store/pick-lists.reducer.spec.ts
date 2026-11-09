import { PickList, QueueItem, ScanOutcome, ageText } from '../models';
import { PickListsApiActions, PickQueuePageActions, PickStationPageActions } from './pick-lists.actions';
import { pickListsReducer } from './pick-lists.reducer';
import { initialPickListsState } from './pick-lists.state';

const item: QueueItem = {
  orderId: 5, orderNumber: 'RMP-5', status: 'QUEUED', queuedAt: '2026-11-09T09:00:00Z', ageMinutes: 12, lineCount: 2, unitCount: 3, claimedBy: null, putBackRequired: false, isCashOnDelivery: true,
};
const list: PickList = {
  orderId: 5, orderNumber: 'RMP-5', status: 'PICKING', claimedBy: 'picker', currentBin: null, nextBin: 'A-01-1', readyToComplete: false, putBackRequired: false,
  rows: [{ lineId: 1, variantId: 9, binCode: 'A-01-1', sku: 'SKU-9', name: 'Floral Dress', colour: 'Red', size: '2-3Y', quantity: 2, picked: 0, status: 'ALLOCATED' }],
};
const refused: ScanOutcome = { accepted: false, code: 'GTIN_NOT_ON_ORDER', message: 'This item is not on the order. Put it back.', nextBin: null, currentBin: 'A-01-1', readyToComplete: false, pickedUnits: 0, totalUnits: 2 };
const conflict = { status: 409, message: 'Picker Two already holds this pick.', fieldErrors: {} };

describe('ageText (spec APP-8)', () => {
  it.each([
    [0, 'just now'],
    [12, '12 min'],
    [60, '1 h'],
    [125, '2 h 5 min'],
  ])('%i minutes reads as %s', (minutes, expected) => {
    expect(ageText(minutes)).toBe(expected);
  });
});

describe('pickListsReducer', () => {
  it('APP-8 opening the queue starts from a clean slate and asks for the first page', () => {
    const state = pickListsReducer({ ...initialPickListsState, notice: 'old', page: 4, queue: [item] }, PickQueuePageActions.opened());

    expect(state.queueLoading).toBe(true);
    expect(state.queue).toEqual([]);
    expect(state.page).toBe(1);
    expect(state.notice).toBeNull();
  });

  it('APP-8 a loaded page fills the queue and the total', () => {
    const state = pickListsReducer(initialPickListsState, PickListsApiActions.queueSucceeded({ result: { items: [item], page: 1, pageSize: 10, totalCount: 31, totalPages: 4 } }));

    expect(state.queue).toEqual([item]);
    expect(state.total).toBe(31);
    expect(state.queueLoading).toBe(false);
  });

  it('PCK-9 opening an order takes the pick, and a claim that works shows the list', () => {
    const opened = pickListsReducer(initialPickListsState, PickStationPageActions.opened({ orderId: 5 }));
    const claimed = pickListsReducer(opened, PickListsApiActions.claimSucceeded({ list }));

    expect(opened.listLoading).toBe(true);
    expect(claimed.list).toEqual(list);
    expect(claimed.heldBy).toBeNull();
  });

  it('PCK-9 a claim refused because someone else holds the order keeps the list read-only and says who', () => {
    const state = pickListsReducer(initialPickListsState, PickListsApiActions.claimRefused({ error: conflict, orderId: 5 }));

    expect(state.heldBy).toBe('Picker Two already holds this pick.');
    expect(state.error).toEqual(conflict);
  });

  it('APP-6 a scan makes the box wait, and the answer is kept to be shown large', () => {
    const waiting = pickListsReducer(initialPickListsState, PickStationPageActions.scanned({ orderId: 5, code: 'A-01-1' }));
    const answered = pickListsReducer(waiting, PickListsApiActions.scanSucceeded({ outcome: refused, orderId: 5 }));

    expect(waiting.busy).toBe(true);
    expect(answered.busy).toBe(false);
    expect(answered.lastScan).toEqual(refused);
  });

  it('APP-6 a scan that got no answer is shown clearly and frees the box, never silently dropped', () => {
    const waiting = pickListsReducer(initialPickListsState, PickStationPageActions.scanned({ orderId: 5, code: 'A-01-1' }));
    const failed = pickListsReducer(waiting, PickListsApiActions.scanFailed({ error: { status: 0, message: 'No answer from the server.', fieldErrors: {} } }));

    expect(failed.busy).toBe(false);
    expect(failed.error?.message).toContain('No answer');
  });

  it('PCK-7 completing the pick says what to do next', () => {
    const state = pickListsReducer({ ...initialPickListsState, busy: true }, PickListsApiActions.completeSucceeded({ result: { completed: true, alreadyDone: false }, orderId: 5 }));

    expect(state.busy).toBe(false);
    expect(state.notice).toContain('pack station');
  });

  it('PEX-1 flagging a line makes the screen wait, and the answer says the line waits for restock', () => {
    const waiting = pickListsReducer(initialPickListsState, PickStationPageActions.exceptionFlagged({ orderId: 5, request: { variantId: 9, binCode: 'A-01-1', reason: 'SHORT', foundCount: 0 } }));
    const done = pickListsReducer(waiting, PickListsApiActions.exceptionSucceeded({ orderId: 5 }));

    expect(waiting.busy).toBe(true);
    expect(done.notice).toContain('restock');
  });

  it('a refreshed list ends the wait', () => {
    const state = pickListsReducer({ ...initialPickListsState, busy: true, raising: true }, PickListsApiActions.listSucceeded({ list }));

    expect(state.busy).toBe(false);
    expect(state.raising).toBe(false);
    expect(state.list).toEqual(list);
  });
});
