import { PackScanOutcome, PackView, WeightOutcome } from '../models';
import { PackQueuePageActions, PackStationApiActions, PackStationPageActions } from './pack-station.actions';
import { packStationReducer } from './pack-station.reducer';
import { initialPackStationState } from './pack-station.state';

const view: PackView = {
  orderId: 5, orderNumber: 'RMP-5', status: 'PACKING', claimedBy: 'packer', isCashOnDelivery: true, cashToCollect: 3200, packaging: 'BOX', expectedGrams: 580,
  lines: [{ lineId: 1, sku: 'SKU-9', name: 'Floral Dress', colour: 'Red', size: '2-3Y', quantity: 2, packed: 0 }], allScanned: false, weightGrams: null, weightDeviates: false, note: null,
};
const refused: PackScanOutcome = { accepted: false, code: 'SURPLUS_UNIT', message: 'There is one unit too many of this item.', allScanned: true, packedUnits: 2, totalUnits: 2 };
const weight: WeightOutcome = { weightGrams: 1200, expectedGrams: 580, allowedGrams: 87, deviationGrams: 620, withinTolerance: false, packaging: 'BOX', message: 'The weight is 620 g over what the order should weigh.' };

describe('packStationReducer (spec PAK-1 to PAK-13)', () => {
  it('PAK-10 opening the queue starts from a clean slate', () => {
    const state = packStationReducer({ ...initialPackStationState, notice: 'old', packed: true }, PackQueuePageActions.opened());

    expect(state.queueLoading).toBe(true);
    expect(state.packed).toBe(false);
    expect(state.notice).toBeNull();
  });

  it('PAK-4 opening an order shows it with the packaging and the expected weight', () => {
    const opened = packStationReducer(initialPackStationState, PackStationPageActions.opened({ orderId: 5 }));
    const state = packStationReducer(opened, PackStationApiActions.openSucceeded({ view }));

    expect(opened.viewLoading).toBe(true);
    expect(state.view).toEqual(view);
    expect(state.blockedReason).toBeNull();
  });

  it('PAK-10 an order that cannot be opened (not picked, or someone else has it) is shown read-only with the reason', () => {
    const state = packStationReducer(initialPackStationState, PackStationApiActions.openRefused({ error: { status: 409, message: 'This order is not fully picked, so it cannot be packed.', fieldErrors: {} }, orderId: 5 }));

    expect(state.blockedReason).toContain('not fully picked');
    expect(state.error).toBeNull();
  });

  it('PAK-2 a refused scan is kept to be shown, and the wait ends when the view is read again', () => {
    const waiting = packStationReducer(initialPackStationState, PackStationPageActions.scanned({ orderId: 5, code: '2000000000017' }));
    const answered = packStationReducer(waiting, PackStationApiActions.scanSucceeded({ outcome: refused }));
    const reloaded = packStationReducer(answered, PackStationApiActions.viewSucceeded({ view }));

    expect(waiting.busy).toBe(true);
    expect(answered.lastScan).toEqual(refused);
    expect(reloaded.busy).toBe(false);
  });

  it('PAK-7 a weight outside the tolerance is kept so the warning stays on screen', () => {
    const state = packStationReducer(initialPackStationState, PackStationApiActions.weightSucceeded({ outcome: weight }));

    expect(state.weight?.withinTolerance).toBe(false);
    expect(state.weight?.message).toContain('over');
  });

  it('PAK-8 a refused weight shows the reason and frees the screen', () => {
    const waiting = packStationReducer(initialPackStationState, PackStationPageActions.weightSubmitted({ orderId: 5, grams: 0 }));
    const failed = packStationReducer(waiting, PackStationApiActions.weightFailed({ error: { status: 409, message: 'The weight must be more than zero grams.', fieldErrors: {} } }));

    expect(failed.busy).toBe(false);
    expect(failed.error?.message).toContain('zero grams');
  });

  it('PAK-9 sealing the parcel says what to do next, and a repeat keeps one packed state', () => {
    const first = packStationReducer(initialPackStationState, PackStationApiActions.completeSucceeded({ result: { packed: true, alreadyDone: false }, orderId: 5 }));
    const second = packStationReducer(first, PackStationApiActions.completeSucceeded({ result: { packed: true, alreadyDone: true }, orderId: 5 }));

    expect(first.packed).toBe(true);
    expect(first.notice).toContain('slip');
    expect(second.packed).toBe(true);
  });
});
