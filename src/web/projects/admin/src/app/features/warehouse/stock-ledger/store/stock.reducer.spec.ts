import { movement, pageOf, stockRow } from '@app/testing/warehouse-fixtures';
import { StockApiActions, StockPageActions } from './stock.actions';
import { stockReducer } from './stock.reducer';
import { initialStockState } from './stock.state';

describe('stockReducer (spec LED-5, LED-6, LED-8, LED-9)', () => {
  it('opening the page starts from a clean slate', () => {
    const state = stockReducer({ ...initialStockState, searchText: 'x', page: 3, historyFor: stockRow() }, StockPageActions.opened());

    expect(state).toMatchObject({ searchText: '', page: 1, historyFor: null, loading: true });
  });

  it('searching goes back to the first page and reloads', () => {
    const state = stockReducer({ ...initialStockState, page: 4 }, StockPageActions.searchChanged({ searchText: 'ROMP' }));

    expect(state).toMatchObject({ searchText: 'ROMP', page: 1, loading: true });
  });

  it('keeps the page of stock with its bins (LED-6)', () => {
    const state = stockReducer({ ...initialStockState, loading: true }, StockApiActions.loadStockSucceeded({ result: pageOf([stockRow()], { totalCount: 40 }) }));

    expect(state).toMatchObject({ total: 40, loading: false });
    expect(state.rows[0].bins).toHaveLength(2);
  });

  it('the movement history opens for one variant, starts on its first page, and closes again (LED-9)', () => {
    const variant = stockRow();
    const asking = stockReducer({ ...initialStockState, movementsPage: 4 }, StockPageActions.historyRequested({ variant }));
    expect(asking).toMatchObject({ historyFor: variant, movementsPage: 1, movementsLoading: true });

    const loaded = stockReducer(asking, StockApiActions.loadMovementsSucceeded({ result: pageOf([movement()], { totalCount: 21, page: 1 }) }));
    expect(loaded).toMatchObject({ movementsTotal: 21, movementsLoading: false });

    const paged = stockReducer(loaded, StockPageActions.historyPageChanged({ page: 2 }));
    expect(paged).toMatchObject({ movementsPage: 2, movementsLoading: true });

    expect(stockReducer(paged, StockPageActions.historyClosed())).toMatchObject({ historyFor: null, movements: [] });
  });

  it('a failed load shows the error and stops the spinner', () => {
    const error = { status: 500, message: 'boom', fieldErrors: {} };

    expect(stockReducer({ ...initialStockState, loading: true }, StockApiActions.loadStockFailed({ error }))).toMatchObject({ error, loading: false });
    expect(stockReducer({ ...initialStockState, movementsLoading: true }, StockApiActions.loadMovementsFailed({ error }))).toMatchObject({ error, movementsLoading: false });
  });
});
