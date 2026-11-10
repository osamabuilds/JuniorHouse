import { CodApiActions, CodPageActions, CodRemittanceActions, codReducer, initialCodState } from './cod.store';
import { RemittanceDetail, RemittanceSummary } from '../models';

const summary: RemittanceSummary = {
  id: 3, courierName: 'Primary courier', reference: 'ST-1', remittedOn: '2026-11-10', statedTotal: 4700, lineCount: 1, flaggedCount: 0, openCount: 0, recordedAt: '2026-11-10T09:00:00Z', recordedBy: 'Finance',
};
const detail: RemittanceDetail = { summary, lines: [] };

describe('COD reconciliation store (spec COD-9, COD-12, COD-13, COD-14)', () => {
  it('COD-13 the summary fills the overview and ends loading', () => {
    const loading = codReducer(initialCodState, CodPageActions.opened());
    const done = codReducer(loading, CodApiActions.summarySucceeded({ summary: { couriers: [], openExceptions: 2 } }));

    expect(loading.loading).toBe(true);
    expect(done.summary?.openExceptions).toBe(2);
    expect(done.loading).toBe(false);
  });

  it('COD-14 each list keeps its own page, and the overdue filter returns the awaiting list to page 1', () => {
    const moved = codReducer(initialCodState, CodPageActions.pageChanged({ list: 'exceptions', page: 3 }));
    const filtered = codReducer(codReducer(moved, CodPageActions.pageChanged({ list: 'awaiting', page: 4 })), CodPageActions.overdueFilterChanged({ overdueOnly: true }));

    expect(moved.exceptions.page).toBe(3);
    expect(moved.awaiting.page).toBe(1);
    expect(filtered.awaiting.page).toBe(1);
    expect(filtered.overdueOnly).toBe(true);
    expect(filtered.exceptions.page).toBe(3);
  });

  it('COD-9 recording again says nothing changed, and a first recording says nothing', () => {
    const waiting = codReducer(initialCodState, CodRemittanceActions.recordSubmitted({ request: { courier: 'PRIMARY', reference: 'ST-1', remittedOn: '2026-11-10', statedTotal: 1, lines: [] } }));
    const again = codReducer(waiting, CodApiActions.recordSucceeded({ detail, alreadyRecorded: true }));
    const first = codReducer(waiting, CodApiActions.recordSucceeded({ detail, alreadyRecorded: false }));

    expect(waiting.busy).toBe(true);
    expect(again.notice).toContain('nothing changed');
    expect(first.notice).toBeNull();
    expect(first.busy).toBe(false);
  });

  it('COD-12 resolving makes the page wait, and success frees it with a notice', () => {
    const waiting = codReducer(initialCodState, CodPageActions.lineResolved({ lineId: 5, note: 'typo' }));
    const done = codReducer(waiting, CodApiActions.resolveSucceeded());

    expect(waiting.busy).toBe(true);
    expect(done.busy).toBe(false);
    expect(done.notice).toContain('resolved');
  });

  it('a failure frees the page and keeps the message', () => {
    const failed = codReducer({ ...initialCodState, busy: true }, CodApiActions.requestFailed({ error: { status: 400, message: 'The stated total does not equal the sum of the lines.' } as never }));

    expect(failed.busy).toBe(false);
    expect(failed.error?.message).toContain('stated total');
  });
});
