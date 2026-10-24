import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Store, provideStore } from '@ngrx/store';
import { draftSettings, navItem } from '@app/testing/storefront-fixtures';
import { ApiError } from '@core/http';
import { StorefrontSettingsApiActions, StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { storefrontSettingsReducer } from '../../store/storefront-settings.reducer';
import { STOREFRONT_SETTINGS_FEATURE_KEY } from '../../store/storefront-settings.state';
import { NavigationPageContainer } from './navigation-page.container';

describe('NavigationPageContainer (spec ST-1, ST-1a, ST-4, ST-14, ST-15)', () => {
  function create() {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideStore({ [STOREFRONT_SETTINGS_FEATURE_KEY]: storefrontSettingsReducer })],
    });
    const store = TestBed.inject(Store);
    const dispatched: unknown[] = [];
    const original = store.dispatch.bind(store);
    store.dispatch = ((action: never) => {
      dispatched.push(action);
      return original(action);
    }) as typeof store.dispatch;

    const fixture = TestBed.createComponent(NavigationPageContainer);
    store.dispatch(StorefrontSettingsApiActions.loadDraftSucceeded({ draft: draftSettings() }));
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const names = () => Array.from(root.querySelectorAll('.nav-tree > li > fieldset input[id$="-label"]')).map((i) => (i as HTMLInputElement).value);
    const button = (name: RegExp) => Array.from(root.querySelectorAll('button')).find((b) => name.test(b.getAttribute('aria-label') ?? b.textContent ?? '')) as HTMLButtonElement;
    const saveButton = () => button(/Save navigation|Saving/);
    return { fixture, root, store, dispatched, names, button, saveButton };
  }

  it('opens by asking for the latest draft and shows the saved tree', () => {
    const { dispatched, names } = create();

    expect(dispatched).toContainEqual(StorefrontSettingsPageActions.draftOpened());
    expect(names()).toEqual(['Girls', 'Sale']);
  });

  it('can be reordered with the keyboard alone: Move down is a button in the tab order (WCAG 2.5.7, ST-15)', () => {
    const { fixture, names, button } = create();

    const moveDown = button(/Move Girls down/);
    expect(moveDown.tabIndex).toBeGreaterThanOrEqual(0);
    moveDown.click();
    fixture.detectChanges();

    expect(names()).toEqual(['Sale', 'Girls']);
  });

  it('indents an entry under the one above it and outdents it again', () => {
    const { fixture, root, button } = create();

    button(/Make Sale a member of the entry above/).click();
    fixture.detectChanges();
    expect(root.querySelectorAll('.nav-tree > li').length).toBe(1);
    expect(root.querySelectorAll('.nav-tree > li > ul > li').length).toBe(2);

    button(/Move Sale out one level/).click();
    fixture.detectChanges();
    expect(root.querySelectorAll('.nav-tree > li').length).toBe(2);
  });

  it('adds an entry and a member, with fresh keys', () => {
    const { fixture, root, button } = create();
    const addEntry = Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Add an entry')) as HTMLButtonElement;

    addEntry.click();
    fixture.detectChanges();
    button(/Add a member to Sale/).click();
    fixture.detectChanges();

    expect(root.querySelectorAll('.nav-tree > li').length).toBe(3);
    expect(root.textContent).toContain('Key: new-entry');
  });

  it('Save stays off until something changed, then sends the whole tree (ST-1)', () => {
    const { fixture, dispatched, button, saveButton } = create();
    expect(saveButton().disabled).toBe(true);

    button(/Move Girls down/).click();
    fixture.detectChanges();
    expect(saveButton().disabled).toBe(false);
    saveButton().click();

    const save = dispatched.find((a) => (a as { type: string }).type === StorefrontSettingsPageActions.saveSubmitted.type) as ReturnType<typeof StorefrontSettingsPageActions.saveSubmitted>;
    expect(save.part.part).toBe('navigation');
    expect((save.part as unknown as { items: { key: string }[] }).items.map((i) => i.key)).toEqual(['sale', 'girls']);
  });

  it('warns about unsaved changes and offers to discard them', () => {
    const { fixture, root, button } = create();
    expect(fixture.componentInstance.hasUnsavedChanges()).toBe(false);

    button(/Move Girls down/).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.hasUnsavedChanges()).toBe(true);
    expect(root.textContent).toContain('You have changes that are not saved');

    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Discard')) as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.hasUnsavedChanges()).toBe(false);
  });

  it('shows the server reasons under the entries and in a banner, and keeps the edits (ST-4)', () => {
    const { fixture, root, store, button } = create();
    button(/Move Girls down/).click();
    fixture.detectChanges();
    const error: ApiError = {
      status: 400,
      message: 'x',
      fieldErrors: { 'navigation.girls.dresses': ['Choose where "Dresses" links to.'], navigation: ['The menu has no links. Add at least one enabled link.'] },
    };

    store.dispatch(StorefrontSettingsApiActions.saveFailed({ error }));
    fixture.detectChanges();

    expect(root.textContent).toContain('Choose where "Dresses" links to.');
    expect(root.textContent).toContain('The menu has no links');
    expect(root.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.componentInstance.hasUnsavedChanges()).toBe(true);
  });

  it('a conflict says someone else saved first and offers a reload (ST-14)', () => {
    const { fixture, root, store, dispatched } = create();

    store.dispatch(StorefrontSettingsApiActions.saveFailed({ error: { status: 409, message: 'Someone else changed the settings since you opened them.', fieldErrors: {} } }));
    fixture.detectChanges();

    expect(root.textContent).toContain('Someone else changed the settings');
    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Reload')) as HTMLButtonElement).click();
    expect(dispatched.filter((a) => (a as { type: string }).type === StorefrontSettingsPageActions.draftOpened.type)).toHaveLength(2);
  });

  it('after a save the saved tree is the new starting point', () => {
    const { fixture, store, button } = create();
    button(/Move Girls down/).click();
    fixture.detectChanges();

    store.dispatch(StorefrontSettingsApiActions.saveSucceeded({ version: 5, patch: { navigation: [navItem({ key: 'sale', label: 'Sale' }), navItem({ key: 'girls', label: 'Girls', sort: 2 })] } }));
    fixture.detectChanges();

    expect(fixture.componentInstance.hasUnsavedChanges()).toBe(false);
  });
});
