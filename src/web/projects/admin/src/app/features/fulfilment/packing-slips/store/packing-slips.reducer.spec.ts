import { PackingSlip } from '../models';
import { PackingSlipApiActions, PackingSlipPageActions } from './packing-slips.actions';
import { packingSlipsReducer } from './packing-slips.reducer';
import { initialPackingSlipsState } from './packing-slips.state';

const slip = { orderNumber: 'RMP-5', isReprint: false, printCount: 0, printPending: false } as unknown as PackingSlip;

describe('packingSlipsReducer (spec SLP-5, SLP-6)', () => {
  it('opening starts from a clean slate and asks for the slip', () => {
    const state = packingSlipsReducer({ ...initialPackingSlipsState, notice: 'old', slip }, PackingSlipPageActions.opened({ orderId: 5 }));

    expect(state.loading).toBe(true);
    expect(state.slip).toBeNull();
    expect(state.notice).toBeNull();
  });

  it('SLP-5 printing makes the button wait, and a print that worked says it was sent', () => {
    const waiting = packingSlipsReducer(initialPackingSlipsState, PackingSlipPageActions.printClicked({ orderId: 5 }));
    const done = packingSlipsReducer(waiting, PackingSlipApiActions.printSucceeded({ result: { printed: true, pending: false, slip: { ...slip, isReprint: true, printCount: 2 } } }));

    expect(waiting.printing).toBe(true);
    expect(done.printing).toBe(false);
    expect(done.notice).toContain('sent to the printer');
    expect(done.slip?.isReprint).toBe(true);
  });

  it('SLP-6 a printer that is down does not block: the screen says the slip is waiting and can still be printed from the page', () => {
    const state = packingSlipsReducer(initialPackingSlipsState, PackingSlipApiActions.printSucceeded({ result: { printed: false, pending: true, slip: { ...slip, printPending: true } } }));

    expect(state.printing).toBe(false);
    expect(state.error).toBeNull();
    expect(state.notice).toContain('will print on its own');
    expect(state.notice).toContain('print this page');
  });

  it('a slip that is not available yet (the order is not at the pack station) shows the reason', () => {
    const state = packingSlipsReducer(initialPackingSlipsState, PackingSlipApiActions.loadFailed({ error: { status: 404, message: 'Not found.', fieldErrors: {} } }));

    expect(state.loading).toBe(false);
    expect(state.error?.status).toBe(404);
  });
});
