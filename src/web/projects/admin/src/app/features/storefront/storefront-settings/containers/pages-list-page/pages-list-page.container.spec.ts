import { createStorefrontContainer } from '@app/testing/storefront-store';
import { pageRow } from '@app/testing/storefront-fixtures';
import { pageOf } from '@app/testing/warehouse-fixtures';
import { buttonNamed, choose, textContent } from '@app/testing/dom-helpers';
import { StorefrontSettingsApiActions, StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { PagesListPageContainer } from './pages-list-page.container';

describe('PagesListPageContainer (spec ST-8)', () => {
  function create() {
    const setup = createStorefrontContainer(PagesListPageContainer, { draft: null });
    const load = (rows = [pageRow(), pageRow({ id: 2, slug: 'terms', title: 'Terms' })], total = rows.length) => {
      setup.store.dispatch(StorefrontSettingsApiActions.loadPagesSucceeded({ result: pageOf(rows, { totalCount: total, pageSize: 10 }) }));
      setup.fixture.detectChanges();
    };
    return { ...setup, load };
  }

  it('opens by asking for the first page of pages', () => {
    const { dispatched } = create();

    expect(dispatched).toContainEqual(StorefrontSettingsPageActions.pagesOpened());
  });

  it('lists the pages as links, and offers a New page link', () => {
    const { root, load } = create();
    load();

    expect(root.querySelectorAll('tbody tr').length).toBe(2);
    expect(root.querySelector('a[href="/storefront/pages/new"]')).not.toBeNull();
    expect(root.querySelector('h1')?.textContent).toBe('Pages');
  });

  it('filters by status through a labelled select and goes back to page 1', () => {
    const { root, last } = create();

    choose(root, 'select', 'Published');

    expect(last(StorefrontSettingsPageActions.pagesStatusFiltered.type)).toEqual(StorefrontSettingsPageActions.pagesStatusFiltered({ status: 'Published' }));
    expect(root.querySelector('label.toolbar__select .sr-only')?.textContent).toBe('Show pages with status');
  });

  it('pages through the list with Next and Previous (paged endpoint)', () => {
    const { fixture, root, load, last } = create();
    load(Array.from({ length: 10 }, (_, i) => pageRow({ id: i + 1, slug: `p-${i}`, title: `Page ${i}` })), 31);

    expect(textContent(root)).toContain('Showing 1–10 of 31');
    buttonNamed(root, 'Next').click();
    fixture.detectChanges();

    expect(last(StorefrontSettingsPageActions.pagesPageChanged.type)).toEqual(StorefrontSettingsPageActions.pagesPageChanged({ page: 2 }));
    expect(textContent(root)).toContain('Page 2 of 4');
  });

  it('shows why the list failed', () => {
    const { fixture, root, store } = create();

    store.dispatch(StorefrontSettingsApiActions.loadPagesFailed({ error: { status: 500, message: 'Something went wrong on our side.', fieldErrors: {} } }));
    fixture.detectChanges();

    expect(root.querySelector('[role="alert"]')?.textContent).toContain('Something went wrong');
  });
});
