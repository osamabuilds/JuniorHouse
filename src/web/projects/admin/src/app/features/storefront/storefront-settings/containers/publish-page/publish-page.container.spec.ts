import { createStorefrontContainer } from '@app/testing/storefront-store';
import { draftSettings, pageRow, snapshotRow } from '@app/testing/storefront-fixtures';
import { pageOf } from '@app/testing/warehouse-fixtures';
import { buttonNamed, hasButton, textContent, typeInto } from '@app/testing/dom-helpers';
import { StorefrontSettingsApiActions, StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { PublishPageContainer } from './publish-page.container';

describe('PublishPageContainer (spec ST-3, ST-12, ST-17)', () => {
  function create() {
    const setup = createStorefrontContainer(PublishPageContainer, {
      draft: draftSettings({ pages: [pageRow({ status: 'Published' }), pageRow({ id: 2, slug: 'terms', status: 'Draft' }), pageRow({ id: 3, slug: 'old', status: 'Archived' })] }),
    });
    setup.store.dispatch(
      StorefrontSettingsApiActions.loadSnapshotsSucceeded({
        result: pageOf([snapshotRow({ versionNo: 3 }), snapshotRow({ versionNo: 2, isCurrent: false, note: 'Earlier' })], { totalCount: 2 }),
      }),
    );
    setup.fixture.detectChanges();
    return setup;
  }

  it('opens by loading the draft and the versions', () => {
    const { dispatched } = create();

    expect(dispatched).toContainEqual(StorefrontSettingsPageActions.publishOpened());
  });

  it('says what will go live: entries, published pages, drafts left out and archived ones', () => {
    const { root } = create();

    expect(textContent(root)).toContain('3 navigation entries');
    expect(textContent(root)).toContain('1 published page, 1 draft (drafts are not shown), 1 archived');
  });

  it('publishes with the note typed, once, and shows the button busy while it runs', () => {
    const { fixture, root, store, last } = create();

    typeInto(root, '#pp-note', '  New delivery charges ');
    buttonNamed(root, 'Publish now').click();

    expect(last(StorefrontSettingsPageActions.publishSubmitted.type)).toEqual(StorefrontSettingsPageActions.publishSubmitted({ note: 'New delivery charges' }));
    store.dispatch(StorefrontSettingsPageActions.publishSubmitted({ note: '' }));
    fixture.detectChanges();
    expect(buttonNamed(root, 'Publishing…').disabled).toBe(true);
  });

  it('reports a new version, or that nothing changed (ST-3)', () => {
    const { fixture, root, store } = create();

    store.dispatch(StorefrontSettingsApiActions.publishSucceeded({ result: { versionNo: 4, documentHash: 'x', created: true } }));
    fixture.detectChanges();

    expect(root.querySelector('[role="status"]')?.textContent).toContain('Published as version 4');
  });

  it('shows the report of everything the server refused, one line per place (ST-3, ST-17)', () => {
    const { fixture, root, store } = create();

    store.dispatch(
      StorefrontSettingsApiActions.publishFailed({
        error: {
          status: 400,
          message: 'x',
          fieldErrors: {
            'footer.Help.Delivery': ['The page "delivery" does not exist or is not published. Publish it or choose another page.'],
            'pages.exchanges': ['The page quotes "exchange.conditions", which is still empty.'],
          },
        },
      }),
    );
    fixture.detectChanges();

    const items = Array.from(root.querySelectorAll('.report li')).map((li) => li.textContent?.replace(/\s+/g, ' ').trim());
    expect(items).toHaveLength(2);
    expect(items[0]).toContain('footer › Help › Delivery');
    expect(items[0]).toContain('is not published');
    expect(items[1]).toContain('pages › exchanges');
    expect(textContent(root)).toContain("It can't be published yet");
  });

  it('marks the version on the shop and offers Restore for the others (ST-12)', () => {
    const { root } = create();

    expect(textContent(root)).toContain('Version 3 on the shop');
    expect(hasButton(root, 'Restore version 2')).toBe(true);
    expect(hasButton(root, 'Restore version 3')).toBe(false);
  });

  it('restoring takes two steps: ask, then confirm, so one stray click cannot do it', () => {
    const { fixture, root, last } = create();

    buttonNamed(root, 'Restore version 2').click();
    fixture.detectChanges();
    expect(last(StorefrontSettingsPageActions.restoreSubmitted.type)).toBeUndefined();
    expect(textContent(root)).toContain('Restore version 2?');
    expect(textContent(root)).toContain('Your current draft is not changed');

    buttonNamed(root, 'Yes, restore version 2').click();
    fixture.detectChanges();

    expect(last(StorefrontSettingsPageActions.restoreSubmitted.type)).toEqual(StorefrontSettingsPageActions.restoreSubmitted({ versionNo: 2, note: '' }));
    expect(textContent(root)).not.toContain('Restore version 2?');
  });

  it('cancelling a restore changes nothing', () => {
    const { fixture, root, last } = create();

    buttonNamed(root, 'Restore version 2').click();
    fixture.detectChanges();
    buttonNamed(root, 'Cancel').click();
    fixture.detectChanges();

    expect(last(StorefrontSettingsPageActions.restoreSubmitted.type)).toBeUndefined();
    expect(textContent(root)).not.toContain('Restore version 2?');
  });

  it('pages through the versions', () => {
    const { fixture, root, store, last } = create();
    store.dispatch(
      StorefrontSettingsApiActions.loadSnapshotsSucceeded({ result: pageOf(Array.from({ length: 10 }, (_, i) => snapshotRow({ versionNo: 30 - i, isCurrent: i === 0 })), { totalCount: 31 }) }),
    );
    fixture.detectChanges();

    buttonNamed(root, 'Next').click();

    expect(last(StorefrontSettingsPageActions.snapshotsPageChanged.type)).toEqual(StorefrontSettingsPageActions.snapshotsPageChanged({ page: 2 }));
  });
});
