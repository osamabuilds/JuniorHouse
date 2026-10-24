import { createStorefrontContainer } from '@app/testing/storefront-store';
import { buttonNamed, textContent, typeInto } from '@app/testing/dom-helpers';
import { StorefrontSettingsApiActions, StorefrontSettingsPageActions } from '../../store/storefront-settings.actions';
import { AgesPageContainer } from './ages-page.container';

describe('AgesPageContainer (spec S-2, ST-2, ST-14)', () => {
  it('opens by asking for the latest draft and lists the saved ages', () => {
    const { dispatched, root } = createStorefrontContainer(AgesPageContainer);

    expect(dispatched).toContainEqual(StorefrontSettingsPageActions.draftOpened());
    expect((root.querySelector('#age-label-0') as HTMLInputElement).value).toBe('1Y');
    expect((root.querySelector('#age-label-1') as HTMLInputElement).value).toBe('2Y');
  });

  it('Save is off until something changes, then sends the ages numbered in the order shown (ST-2)', () => {
    const { fixture, root, last } = createStorefrontContainer(AgesPageContainer);
    expect(buttonNamed(root, 'Save ages').disabled).toBe(true);

    buttonNamed(root, 'Move the 2Y age up').click();
    fixture.detectChanges();
    buttonNamed(root, 'Save ages').click();

    const action = last(StorefrontSettingsPageActions.saveSubmitted.type) as ReturnType<typeof StorefrontSettingsPageActions.saveSubmitted>;
    const steps = (action.part as unknown as { steps: { code: string; sort: number }[] }).steps;
    expect(steps.map((s) => [s.code, s.sort])).toEqual([['2y', 1], ['1y', 2]]);
  });

  it('adds an age with a code to type, and the saved codes stay locked', () => {
    const { fixture, root, instance } = createStorefrontContainer(AgesPageContainer);

    buttonNamed(root, 'Add an age').click();
    fixture.detectChanges();
    typeInto(root, '#age-code-2', '3y');
    fixture.detectChanges();
    typeInto(root, '#age-label-2', '3Y');
    fixture.detectChanges();

    expect((root.querySelector('#age-code-0') as HTMLInputElement).readOnly).toBe(true);
    expect(instance.steps().map((s) => s.code)).toEqual(['1y', '2y', '3y']);
    expect(instance.hasUnsavedChanges()).toBe(true);
  });

  it('shows the server reason for an age and keeps the edits (ST-4)', () => {
    const { fixture, root, store, instance } = createStorefrontContainer(AgesPageContainer);
    buttonNamed(root, 'Add an age').click();
    fixture.detectChanges();

    store.dispatch(
      StorefrontSettingsApiActions.saveFailed({
        error: { status: 400, message: 'x', fieldErrors: { 'ages.1y': ['The age code "1y" is used twice.'] } },
      }),
    );
    fixture.detectChanges();

    expect(textContent(root)).toContain('The age code "1y" is used twice.');
    expect(instance.hasUnsavedChanges()).toBe(true);
  });

  it('discarding puts the saved ages back', () => {
    const { fixture, root, instance } = createStorefrontContainer(AgesPageContainer);
    buttonNamed(root, 'Move the 2Y age up').click();
    fixture.detectChanges();

    buttonNamed(root, 'Discard changes').click();
    fixture.detectChanges();

    expect(instance.steps().map((s) => s.code)).toEqual(['1y', '2y']);
    expect(instance.hasUnsavedChanges()).toBe(false);
  });
});
