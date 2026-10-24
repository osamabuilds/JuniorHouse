import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { block, contentPage } from '@app/testing/storefront-fixtures';
import { createStorefrontContainer } from '@app/testing/storefront-store';
import { buttonNamed, hasButton, textContent, typeInto } from '@app/testing/dom-helpers';
import { StorefrontSettingsApiActions, StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { PageEditorPageContainer } from './page-editor-page.container';

describe('PageEditorPageContainer (spec ST-8, ST-9, ST-10, ST-14, ST-17)', () => {
  function create(id: string | null, page = contentPage()) {
    const setup = createStorefrontContainer(PageEditorPageContainer, {
      draft: null,
      providers: [{ provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } } }],
    });
    if (id) {
      setup.store.dispatch(StorefrontSettingsApiActions.loadPageSucceeded({ page }));
      setup.fixture.detectChanges();
    }

    return setup;
  }

  it('a new page starts blank, in a form with labelled fields, and can only be created', () => {
    const { root, instance, dispatched } = create(null);

    expect(root.querySelector('h1')?.textContent).toBe('New page');
    expect(instance.isNew()).toBe(true);
    expect(dispatched).toContainEqual(StorefrontSettingsPageActions.pageEditorOpened({ id: null }));
    for (const id of ['pe-title', 'pe-slug', 'pe-description']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }

    expect(hasButton(root, /Mark as published/)).toBe(false);
  });

  it('opens an existing page by its id in the address and shows it', () => {
    const { root, dispatched } = create('1');

    expect(dispatched).toContainEqual(StorefrontSettingsPageActions.pageEditorOpened({ id: 1 }));
    expect((root.querySelector('#pe-title') as HTMLInputElement).value).toBe('Delivery');
    expect((root.querySelector('#pe-slug') as HTMLInputElement).value).toBe('delivery');
    expect(textContent(root)).toContain('Status: Draft');
  });

  it('saves the page with a clean address and without empty blocks', () => {
    const { fixture, root, last } = create(null);

    typeInto(root, '#pe-title', ' Care guide ');
    typeInto(root, '#pe-slug', ' Care-Guide ');
    typeInto(root, '#pe-description', 'How to look after clothes.');
    fixture.detectChanges();
    buttonNamed(root, 'Add this block').click();
    fixture.detectChanges();
    buttonNamed(root, 'Create the page').click();

    const action = last(StorefrontSettingsPageActions.pageSaveSubmitted.type) as ReturnType<typeof StorefrontSettingsPageActions.pageSaveSubmitted>;
    expect(action.value).toMatchObject({ slug: 'care-guide', title: 'Care guide', metaDescription: 'How to look after clothes.' });
    expect(action.value.blocks).toEqual([]); // the empty paragraph was dropped
  });

  it('counts the characters left in the search description (160)', () => {
    const { fixture, root } = create(null);

    typeInto(root, '#pe-description', 'x'.repeat(100));
    fixture.detectChanges();

    expect(textContent(root)).toContain('60 characters left');
  });

  it('warns that renaming a live page sends its old address on to the new one (ST-9)', () => {
    const { fixture, root } = create('1', contentPage({ status: 'Published' }));

    typeInto(root, '#pe-slug', 'shipping');
    fixture.detectChanges();

    expect(textContent(root)).toContain('Anyone who opens /delivery will be sent on to /shipping.');
  });

  it('does not warn for a draft, which has no address anyone knows yet', () => {
    const { fixture, root } = create('1');

    typeInto(root, '#pe-slug', 'shipping');
    fixture.detectChanges();

    expect(textContent(root)).not.toContain('will be sent on');
  });

  it('status changes are buttons that wait until the edits are saved (ST-17)', () => {
    const { fixture, root, last } = create('1');
    buttonNamed(root, 'Mark as published').click();
    expect(last(StorefrontSettingsPageActions.pageStatusSubmitted.type)).toEqual(StorefrontSettingsPageActions.pageStatusSubmitted({ status: 'Published' }));

    typeInto(root, '#pe-title', 'Delivery and returns');
    fixture.detectChanges();

    expect(buttonNamed(root, /Mark as published/).disabled).toBe(true);
    expect(buttonNamed(root, 'Archive').disabled).toBe(true);
    expect(textContent(root)).toContain('Save your changes before you change the status.');
  });

  it('offers return to draft for a published page, and no publish button', () => {
    const { root } = create('1', contentPage({ status: 'Published' }));

    expect(hasButton(root, 'Return to draft')).toBe(true);
    expect(hasButton(root, /Mark as published/)).toBe(false);
  });

  it('lists every reason the server gave when it will not publish the page (ST-17)', () => {
    const { fixture, root, store } = create('1');

    store.dispatch(
      StorefrontSettingsApiActions.savePageFailed({
        error: {
          status: 400,
          message: 'x',
          fieldErrors: { 'pages.exchanges': ['The page quotes "exchange.conditions", which is still empty. Fill it in under Settings before publishing the page.'] },
        },
      }),
    );
    fixture.detectChanges();

    expect(textContent(root)).toContain("This can't be saved yet");
    expect(textContent(root)).toContain('which is still empty');
  });

  it('a stale page says someone else saved first (ST-14)', () => {
    const { fixture, root, store } = create('1');

    store.dispatch(StorefrontSettingsApiActions.savePageFailed({ error: { status: 409, message: 'Someone else changed this record while you were working.', fieldErrors: {} } }));
    fixture.detectChanges();

    expect(textContent(root)).toContain('Someone else changed this record');
    expect(textContent(root)).toContain('Reload this page');
  });

  it('keeps markup typed into a block as text, never as elements (ST-10)', () => {
    const { fixture, root, instance } = create('1', contentPage({ blocks: [block({ text: '<img src=x onerror=alert(1)>' })] }));
    fixture.detectChanges();

    expect(root.querySelector('img')).toBeNull();
    expect((root.querySelector('#blk-0-text') as HTMLTextAreaElement).value).toBe('<img src=x onerror=alert(1)>');
    expect(instance.hasUnsavedChanges()).toBe(false);
  });

  it('asks before leaving with unsaved edits', () => {
    const { fixture, root, instance } = create('1');

    typeInto(root, '#pe-title', 'Changed');
    fixture.detectChanges();

    expect(instance.hasUnsavedChanges()).toBe(true);
  });
});
