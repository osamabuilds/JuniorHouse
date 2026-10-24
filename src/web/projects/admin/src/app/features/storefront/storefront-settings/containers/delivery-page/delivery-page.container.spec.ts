import { draftSettings } from '@app/testing/storefront-fixtures';
import { createStorefrontContainer } from '@app/testing/storefront-store';
import { buttonNamed, typeInto } from '@app/testing/dom-helpers';
import { StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { DeliveryPageContainer } from './delivery-page.container';

describe('DeliveryPageContainer (spec ST-7, ST-4)', () => {
  function saved(last: (type: string) => unknown) {
    const action = last(StorefrontSettingsPageActions.saveSubmitted.type) as ReturnType<typeof StorefrontSettingsPageActions.saveSubmitted>;
    return action.part as unknown as { part: string; rules: { city: string; charge: number }[]; exchange: { windowDays: number; conditions: string[] } | null };
  }

  it('saves the rules and the exchange policy together', () => {
    const { fixture, root, last } = createStorefrontContainer(DeliveryPageContainer);

    typeInto(root, '#dr-charge-0', '300');
    fixture.detectChanges();
    buttonNamed(root, 'Save delivery and exchange').click();

    const part = saved(last);
    expect(part.part).toBe('delivery');
    expect(part.rules[0]).toMatchObject({ city: 'Lahore', charge: 300 });
    expect(part.exchange).toEqual({ windowDays: 7, conditions: ['Unworn.'] });
  });

  it('adds a blank city row to fill in', () => {
    const { fixture, root, instance } = createStorefrontContainer(DeliveryPageContainer);

    buttonNamed(root, 'Add a city').click();
    fixture.detectChanges();

    expect(instance.values().rules).toHaveLength(2);
    expect(instance.hasUnsavedChanges()).toBe(true);
  });

  it('with no window and no conditions it saves no policy', () => {
    const { fixture, root, last } = createStorefrontContainer(DeliveryPageContainer, { draft: draftSettings({ exchange: null }) });

    buttonNamed(root, 'Add a city').click();
    fixture.detectChanges();
    buttonNamed(root, 'Save delivery and exchange').click();

    expect(saved(last).exchange).toBeNull();
  });
});
