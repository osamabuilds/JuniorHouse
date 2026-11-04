import { NotifyDemandRow } from '../models';
import { NotifyDemandApiActions, NotifyDemandPageActions } from './notify-demand.actions';
import { notifyDemandReducer } from './notify-demand.reducer';
import { initialNotifyDemandState } from './notify-demand.state';

const row: NotifyDemandRow = {
  variantId: 11, productName: 'Floral dress', colourName: 'Navy', sizeName: '4-5 years', skuCode: 'SKU-11', waiting: 3, oldestRequestAt: '2026-11-01T09:00:00Z',
};
const apiError = { status: 500, message: 'Something went wrong.', fieldErrors: {} };

describe('notifyDemandReducer', () => {
  it('NTM-11 opening starts from a clean slate and asks for the list', () => {
    const state = notifyDemandReducer({ ...initialNotifyDemandState, page: 4, total: 9, error: apiError }, NotifyDemandPageActions.opened());

    expect(state.loading).toBe(true);
    expect(state.page).toBe(1);
    expect(state.total).toBe(0);
    expect(state.error).toBeNull();
  });

  it('NTM-11 a loaded page fills the table and the total', () => {
    const state = notifyDemandReducer(
      { ...initialNotifyDemandState, loading: true },
      NotifyDemandApiActions.loadSucceeded({ result: { items: [row], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }),
    );

    expect(state.rows).toEqual([row]);
    expect(state.total).toBe(1);
    expect(state.loading).toBe(false);
  });

  it('NTM-13 changing the page size goes back to the first page', () => {
    const state = notifyDemandReducer({ ...initialNotifyDemandState, page: 3 }, NotifyDemandPageActions.pageSizeChanged({ pageSize: 50 }));

    expect(state).toMatchObject({ page: 1, pageSize: 50, loading: true });
  });

  it('NTM-11 changing the page asks for that page', () => {
    expect(notifyDemandReducer(initialNotifyDemandState, NotifyDemandPageActions.pageChanged({ page: 2 }))).toMatchObject({ page: 2, loading: true });
  });

  it('NTM-11 a failed load keeps the error and stops loading', () => {
    const state = notifyDemandReducer({ ...initialNotifyDemandState, loading: true }, NotifyDemandApiActions.loadFailed({ error: apiError }));

    expect(state.error).toEqual(apiError);
    expect(state.loading).toBe(false);
  });
});
