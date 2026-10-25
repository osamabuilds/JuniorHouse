import { createSearchContainer, synonymGroup } from '@app/testing/search-fixtures';
import { buttonNamed, controlsWithoutLabels, setChecked, textContent, typeInto } from '@app/testing/dom-helpers';
import { pageOf } from '@app/testing/warehouse-fixtures';
import { SearchAdminApiActions, SearchAdminPageActions } from '../../store/search.actions';
import { SynonymsPageContainer } from './synonyms-page.container';

describe('SynonymsPageContainer (spec SRC-12)', () => {
  function create() {
    const setup = createSearchContainer(SynonymsPageContainer);
    const load = (groups = [synonymGroup(), synonymGroup({ id: 2, name: 'kurta', terms: ['kurta', 'kameez'], isActive: false })]) => {
      setup.store.dispatch(SearchAdminApiActions.loadSynonymsSucceeded({ result: pageOf(groups, { totalCount: groups.length }) }));
      setup.fixture.detectChanges();
    };
    return { ...setup, load };
  }

  it('opens by asking for the groups and has one h1 and two labelled navigations', () => {
    const { dispatched, root } = create();

    expect(dispatched).toContainEqual(SearchAdminPageActions.synonymsOpened());
    expect(root.querySelectorAll('h1').length).toBe(1);
    expect([...root.querySelectorAll('nav')].map((n) => n.getAttribute('aria-label'))).toEqual(['Storefront', 'Search']);
    expect(root.querySelector('a[href="/storefront/search/misses"]')).not.toBeNull();
  });

  it('lists each group with its words and whether it is in use', () => {
    const { root, load } = create();
    load();

    const rows = [...root.querySelectorAll('tbody tr')].map((r) => textContent(r as HTMLElement));
    expect(rows[0]).toContain('dress');
    expect(rows[0]).toContain('dress, frock, gown');
    expect(rows[0]).toContain('In use');
    expect(rows[1]).toContain('Switched off');
  });

  it('adds a group: the form has labelled fields, and saving sends the parsed words', () => {
    const { root, fixture, last, load } = create();
    load();

    buttonNamed(root, 'Add synonym group').click();
    fixture.detectChanges();
    typeInto(root, '#synonym-name', ' bags ');
    typeInto(root, '#synonym-terms', 'bag\ntote, satchel');
    buttonNamed(root, 'Save').click();

    expect(last(SearchAdminPageActions.createSubmitted.type)).toEqual(
      SearchAdminPageActions.createSubmitted({ value: { name: 'bags', isActive: true, terms: ['bag', 'tote', 'satchel'] } }),
    );
    expect(controlsWithoutLabels(root)).toEqual([]);
  });

  it('refuses an empty form in the browser, with a plain message and no request', () => {
    const { root, fixture, dispatched, load } = create();
    load();
    buttonNamed(root, 'Add synonym group').click();
    fixture.detectChanges();

    buttonNamed(root, 'Save').click();
    fixture.detectChanges();

    expect(dispatched.some((a) => a.type === SearchAdminPageActions.createSubmitted.type)).toBe(false);
    expect(root.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('edits a group: the form is filled and the save quotes the row version', () => {
    const { root, fixture, last, load } = create();
    load();

    buttonNamed(root, /^\s*Edit/).click();
    fixture.detectChanges();
    expect((root.querySelector('#synonym-name') as HTMLInputElement).value).toBe('dress');
    expect((root.querySelector('#synonym-terms') as HTMLTextAreaElement).value).toBe('dress\nfrock\ngown');
    setChecked(root, 'input[type="checkbox"]', false);
    buttonNamed(root, 'Save').click();

    expect(last(SearchAdminPageActions.updateSubmitted.type)).toEqual(
      SearchAdminPageActions.updateSubmitted({ id: 1, rowVersion: 11, value: { name: 'dress', isActive: false, terms: ['dress', 'frock', 'gown'] } }),
    );
  });

  it('shows the server messages next to the words field', () => {
    const { root, fixture, store, load } = create();
    load();
    buttonNamed(root, 'Add synonym group').click();
    store.dispatch(SearchAdminApiActions.saveFailed({ error: { status: 400, message: 'Check the form.', fieldErrors: { terms: ['"frock" already belongs to another group.'] } } }));
    fixture.detectChanges();

    expect(textContent(root)).toContain('"frock" already belongs to another group.');
  });

  it('deleting asks twice, naming the group', () => {
    const { root, fixture, last, load } = create();
    load();

    buttonNamed(root, /^\s*Delete/).click();
    fixture.detectChanges();
    expect(last(SearchAdminPageActions.deleteConfirmed.type)).toBeUndefined();
    buttonNamed(root, 'Delete dress').click();

    expect(last(SearchAdminPageActions.deleteConfirmed.type)).toEqual(SearchAdminPageActions.deleteConfirmed({ id: 1 }));
  });
});
