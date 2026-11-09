import { ExceptionRow } from '../models';
import { ExceptionsApiActions, ExceptionsPageActions } from './exceptions.actions';
import { exceptionsReducer } from './exceptions.reducer';
import { initialExceptionsState } from './exceptions.state';

const row: ExceptionRow = {
  id: 3, orderId: 5, orderNumber: 'RMP-5', sku: 'SKU-9', binCode: 'A-01-1', reason: 'SHORT', status: 'OPEN', systemCount: 3, foundCount: 1, raisedBy: 'picker',
  raisedAt: '2026-11-09T09:00:00Z', resolvedBy: null, resolvedAt: null,
};

describe('exceptionsReducer (spec PEX-4, PEX-8, PEX-9)', () => {
  it('PEX-4 opening starts from a clean slate showing open exceptions', () => {
    const state = exceptionsReducer({ ...initialExceptionsState, notice: 'old', page: 3, openOnly: false }, ExceptionsPageActions.opened());

    expect(state.loading).toBe(true);
    expect(state.page).toBe(1);
    expect(state.openOnly).toBe(true);
    expect(state.notice).toBeNull();
  });

  it('PEX-4 a loaded page fills the queue and the total', () => {
    const state = exceptionsReducer(initialExceptionsState, ExceptionsApiActions.loadSucceeded({ result: { items: [row], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));

    expect(state.rows).toEqual([row]);
    expect(state.total).toBe(1);
    expect(state.loading).toBe(false);
  });

  it('changing the filter goes back to the first page', () => {
    const state = exceptionsReducer({ ...initialExceptionsState, page: 4 }, ExceptionsPageActions.filterChanged({ openOnly: false }));

    expect(state.page).toBe(1);
    expect(state.openOnly).toBe(false);
    expect(state.loading).toBe(true);
  });

  it('PEX-8 resolving makes that row wait, and success says which order it was', () => {
    const waiting = exceptionsReducer(initialExceptionsState, ExceptionsPageActions.resolveConfirmed({ id: 3, action: 'restocked' }));
    const done = exceptionsReducer(waiting, ExceptionsApiActions.resolveSucceeded({ row: { ...row, status: 'RESOLVED' } }));

    expect(waiting.resolvingId).toBe(3);
    expect(done.resolvingId).toBeNull();
    expect(done.notice).toContain('RMP-5');
  });

  it('PEX-8 a resolve that is refused (still short) shows the reason and frees the row', () => {
    const waiting = exceptionsReducer(initialExceptionsState, ExceptionsPageActions.resolveConfirmed({ id: 3, action: 'restocked' }));
    const failed = exceptionsReducer(waiting, ExceptionsApiActions.resolveFailed({ error: { status: 409, message: 'Still short: 1 of 3 units are in stock.', fieldErrors: {} } }));

    expect(failed.resolvingId).toBeNull();
    expect(failed.error?.message).toContain('Still short');
  });
});
