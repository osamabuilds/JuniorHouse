import { createStorefrontContainer } from '@app/testing/storefront-store';
import { buttonNamed, typeInto } from '@app/testing/dom-helpers';
import { StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { ContactPageContainer } from './contact-page.container';

describe('ContactPageContainer (spec S-3, ST-4)', () => {
  it('shows the saved values and keeps Save off until something changes', () => {
    const { root } = createStorefrontContainer(ContactPageContainer);

    expect((root.querySelector('#cf-name') as HTMLInputElement).value).toBe('Romp');
    expect(buttonNamed(root, 'Save settings').disabled).toBe(true);
  });

  it('saves every setting, the cleaned searches, and quotes nothing it did not change', () => {
    const { fixture, root, last } = createStorefrontContainer(ContactPageContainer);

    typeInto(root, '#cf-email', ' hello@example.com ');
    fixture.detectChanges();
    buttonNamed(root, 'Add a search term').click();
    fixture.detectChanges();
    buttonNamed(root, 'Save settings').click();

    const action = last(StorefrontSettingsPageActions.saveSubmitted.type) as ReturnType<typeof StorefrontSettingsPageActions.saveSubmitted>;
    const part = action.part as unknown as { part: string; items: { key: string; value: string }[]; popularSearches: string[] };
    expect(part.part).toBe('contact');
    expect(part.items.find((i) => i.key === 'Email')?.value).toBe('hello@example.com');
    expect(part.items.find((i) => i.key === 'Whatsapp')?.value).toBe('+923315613217');
    expect(part.popularSearches).toEqual(['Dresses', 'Shorts']); // the blank new term is dropped
  });

  it('warns about unsaved changes', () => {
    const { fixture, root, instance } = createStorefrontContainer(ContactPageContainer);

    typeInto(root, '#cf-tagline', 'Clothes for kids');
    fixture.detectChanges();

    expect(instance.hasUnsavedChanges()).toBe(true);
  });
});
