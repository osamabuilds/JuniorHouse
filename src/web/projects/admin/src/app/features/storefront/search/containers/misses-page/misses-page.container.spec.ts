import { createSearchContainer, missRow } from '@app/testing/search-fixtures';
import { textContent } from '@app/testing/dom-helpers';
import { pageOf } from '@app/testing/warehouse-fixtures';
import { SearchAdminApiActions, SearchAdminPageActions } from '../../store/search.actions';
import { MissesPageContainer } from './misses-page.container';

describe('MissesPageContainer (spec SRC-13)', () => {
  it('opens by asking for the first page of the log', () => {
    const { dispatched } = createSearchContainer(MissesPageContainer);

    expect(dispatched).toContainEqual(SearchAdminPageActions.missesOpened());
  });

  it('lists the searches that found nothing with the day and the count, as a table with row headers', () => {
    const { root, store, fixture } = createSearchContainer(MissesPageContainer);
    store.dispatch(SearchAdminApiActions.loadMissesSucceeded({ result: pageOf([missRow(), missRow({ text: 'tutu', count: 1, day: '2026-10-24' })], { totalCount: 2 }) }));
    fixture.detectChanges();

    const rows = [...root.querySelectorAll('tbody tr')].map((r) => textContent(r as HTMLElement));
    expect(rows[0]).toContain('ghagra');
    expect(rows[0]).toContain('25 Oct 2026');
    expect(rows[0]).toContain('5');
    expect(root.querySelectorAll('tbody th[scope="row"]').length).toBe(2);
    expect(root.querySelector('caption')?.textContent).toContain('found nothing');
  });

  it('says so when nothing has come back empty, and explains what is kept', () => {
    const { root, store, fixture } = createSearchContainer(MissesPageContainer);
    store.dispatch(SearchAdminApiActions.loadMissesSucceeded({ result: pageOf([]) }));
    fixture.detectChanges();

    expect(textContent(root)).toContain('No searches have come back empty yet.');
    expect(textContent(root)).toContain('Only the words are kept');
  });

  it('shows a failure to load as an alert', () => {
    const { root, store, fixture } = createSearchContainer(MissesPageContainer);
    store.dispatch(SearchAdminApiActions.loadMissesFailed({ error: { status: 500, message: 'The server is down.', fieldErrors: {} } }));
    fixture.detectChanges();

    expect(root.querySelector('[role="alert"]')?.textContent).toContain('The server is down.');
  });
});
