import { pageOf } from '@app/testing/warehouse-fixtures';
import { ApiError } from '@core/http';
import { SizeChartDto, SizeChartRowDto } from '../models';
import { SizeChartsApiActions, SizeChartsPageActions } from './size-charts.actions';
import { sizeChartsReducer } from './size-charts.reducer';
import { initialSizeChartsState } from './size-charts.state';

const row: SizeChartRowDto = { id: 4, categoryId: 3, genderId: 2, name: 'Girls dresses', lineCount: 2 };
const chart: SizeChartDto = { id: 4, categoryId: 3, genderId: 2, name: 'Girls dresses', unit: 'cm', lines: [], version: 55 };
const conflict: ApiError = { status: 409, message: 'There is already a size chart for that category and gender. Edit it instead.', fieldErrors: {} };

describe('sizeChartsReducer (spec CHT-2, CHT-5, CHT-8)', () => {
  it('opening the page starts from a clean slate', () => {
    const state = sizeChartsReducer({ ...initialSizeChartsState, mode: 'edit', page: 3, notice: 'old' }, SizeChartsPageActions.opened());

    expect(state).toMatchObject({ mode: 'list', page: 1, notice: null, loading: true });
  });

  it('keeps the page of charts and the total (CHT-8)', () => {
    const state = sizeChartsReducer(initialSizeChartsState, SizeChartsApiActions.loadListSucceeded({ result: pageOf([row], { totalCount: 12 }) }));

    expect(state).toMatchObject({ total: 12, loading: false });
  });

  it('starting a new chart opens the editor empty', () => {
    const state = sizeChartsReducer({ ...initialSizeChartsState, chart }, SizeChartsPageActions.createStarted());

    expect(state).toMatchObject({ mode: 'create', chart: null });
  });

  it('opening a chart waits for it, then holds it with its version', () => {
    const waiting = sizeChartsReducer(initialSizeChartsState, SizeChartsPageActions.editOpened({ id: 4 }));
    expect(waiting).toMatchObject({ mode: 'edit', chart: null });

    expect(sizeChartsReducer(waiting, SizeChartsApiActions.loadChartSucceeded({ chart })).chart?.version).toBe(55);
  });

  it('a saved chart closes the editor, reloads the list and says products will show it (CHT-2)', () => {
    const state = sizeChartsReducer({ ...initialSizeChartsState, mode: 'edit', saving: true, chart }, SizeChartsApiActions.saveSucceeded({ chart }));

    expect(state).toMatchObject({ mode: 'list', saving: false, loading: true, chart: null });
    expect(state.notice).toContain('Girls dresses');
  });

  it('a refused save (a duplicate, or a stale version) stays in the form with the reason (CHT-5, CHT-2)', () => {
    const state = sizeChartsReducer({ ...initialSizeChartsState, mode: 'create', saving: true }, SizeChartsApiActions.saveFailed({ error: conflict }));

    expect(state).toMatchObject({ mode: 'create', saving: false, formError: conflict });
  });

  it('a deleted chart returns to the list and says what products now show (CHT-3)', () => {
    const state = sizeChartsReducer({ ...initialSizeChartsState, mode: 'edit', chart }, SizeChartsApiActions.deleteSucceeded());

    expect(state.mode).toBe('list');
    expect(state.notice).toContain('size chart coming soon');
  });

  it('changing the page size goes back to page 1', () => {
    expect(sizeChartsReducer({ ...initialSizeChartsState, page: 4 }, SizeChartsPageActions.pageSizeChanged({ pageSize: 50 }))).toMatchObject({ page: 1, pageSize: 50 });
  });
});
