import { createStorefrontContainer } from '@app/testing/storefront-store';
import { buttonNamed, typeInto } from '@app/testing/dom-helpers';
import { StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { FooterPageContainer } from './footer-page.container';

describe('FooterPageContainer (spec S-3, ST-1)', () => {
  it('lists the saved columns and links', () => {
    const { root } = createStorefrontContainer(FooterPageContainer);

    expect((root.querySelector('#fc-title-0') as HTMLInputElement).value).toBe('Help');
    expect((root.querySelector('#fl-label-0-0') as HTMLInputElement).value).toBe('Delivery');
  });

  it('saves the footer with positions that follow the order shown', () => {
    const { fixture, root, last } = createStorefrontContainer(FooterPageContainer);

    buttonNamed(root, 'Add a column').click();
    fixture.detectChanges();
    typeInto(root, '#fc-title-1', 'Shop');
    fixture.detectChanges();
    buttonNamed(root, 'Save footer').click();

    const action = last(StorefrontSettingsPageActions.saveSubmitted.type) as ReturnType<typeof StorefrontSettingsPageActions.saveSubmitted>;
    const columns = (action.part as unknown as { columns: { title: string; sort: number }[] }).columns;
    expect(columns.map((c) => [c.title, c.sort])).toEqual([['Help', 1], ['Shop', 2]]);
  });

  it('warns about unsaved changes and offers to discard them', () => {
    const { fixture, root, instance } = createStorefrontContainer(FooterPageContainer);
    expect(instance.hasUnsavedChanges()).toBe(false);

    typeInto(root, '#fc-title-0', 'Support');
    fixture.detectChanges();
    expect(instance.hasUnsavedChanges()).toBe(true);

    buttonNamed(root, 'Discard changes').click();
    fixture.detectChanges();
    expect((root.querySelector('#fc-title-0') as HTMLInputElement).value).toBe('Help');
  });
});
