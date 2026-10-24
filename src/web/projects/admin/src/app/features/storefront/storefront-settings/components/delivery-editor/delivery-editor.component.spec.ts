import { TestBed } from '@angular/core/testing';
import { draftSettings } from '@app/testing/storefront-fixtures';
import { buttonNamed, controlsWithoutLabels, setChecked, textContent, typeInto } from '@app/testing/dom-helpers';
import { DeliveryValues, toDeliveryValues } from '../../utils/delivery.util';
import { DeliveryEditorComponent } from './delivery-editor.component';

describe('DeliveryEditorComponent (spec ST-7, ST-4, ST-15)', () => {
  function create(values: DeliveryValues = toDeliveryValues(draftSettings()), errors: Record<string, string[]> = {}) {
    const fixture = TestBed.createComponent(DeliveryEditorComponent);
    fixture.componentRef.setInput('values', values);
    fixture.componentRef.setInput('errors', errors);
    const emitted: DeliveryValues[] = [];
    let added = 0;
    fixture.componentInstance.valuesChange.subscribe((v) => emitted.push(v));
    fixture.componentInstance.addRule.subscribe(() => added++);
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted, added: () => added };
  }

  it('every field has a visible label (WCAG 1.3.1)', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('shows the saved city row and the exchange policy', () => {
    const { root } = create();

    expect((root.querySelector('#dr-city-0') as HTMLInputElement).value).toBe('Lahore');
    expect((root.querySelector('#dr-charge-0') as HTMLInputElement).value).toBe('250');
    expect((root.querySelector('#de-window') as HTMLInputElement).value).toBe('7');
    expect((root.querySelector('#de-cond-0') as HTMLInputElement).value).toBe('Unworn.');
  });

  it('money boxes allow paisa and nothing negative (ST-4)', () => {
    const { root } = create();

    const charge = root.querySelector('#dr-charge-0') as HTMLInputElement;
    expect(charge.getAttribute('step')).toBe('0.01');
    expect(charge.getAttribute('min')).toBe('0');
  });

  it('reads the charge as a number and the free-delivery amount as optional', () => {
    const { root, emitted } = create();

    typeInto(root, '#dr-charge-0', '199.50');
    typeInto(root, '#dr-free-0', '');

    expect(emitted[0].rules[0].charge).toBe(199.5);
    expect(emitted[1].rules[0].freeOver).toBeNull();
  });

  it('days are whole numbers', () => {
    const { root, emitted } = create();

    typeInto(root, '#dr-min-0', '2.9');

    expect(emitted[0].rules[0].minDays).toBe(2);
  });

  it('switches a city off, and adds a city through the screen', () => {
    const { root, emitted, added } = create();

    setChecked(root, '.card input[type=checkbox]', false);
    buttonNamed(root, 'Add a city').click();

    expect(emitted[0].rules[0].enabled).toBe(false);
    expect(added()).toBe(1);
  });

  it('blank window means no policy yet; a typed window is a whole number', () => {
    const { root, emitted } = create();

    typeInto(root, '#de-window', '');
    typeInto(root, '#de-window', '14.7');

    expect(emitted[0].windowDays).toBeNull();
    expect(emitted[1].windowDays).toBe(14);
  });

  it('adds, edits, reorders and removes conditions with buttons', () => {
    const { root, emitted } = create({ ...toDeliveryValues(draftSettings()), conditions: ['One.', 'Two.'] });

    buttonNamed(root, 'Add a condition').click();
    typeInto(root, '#de-cond-0', 'First.');
    buttonNamed(root, 'Move condition 2 up').click();
    buttonNamed(root, 'Remove condition 1').click();

    expect(emitted[0].conditions).toEqual(['One.', 'Two.', '']);
    expect(emitted[1].conditions).toEqual(['First.', 'Two.']);
    expect(emitted[2].conditions).toEqual(['Two.', 'One.']);
    expect(emitted[3].conditions).toEqual(['Two.']);
  });

  it('shows the server reasons under the city and under the exchange window (ST-4)', () => {
    const { root } = create(undefined, {
      'delivery.Lahore': ['The delivery charge cannot be negative.'],
      'exchange.window': ['Enter an exchange window of 1 to 365 days.'],
    });

    expect(textContent(root)).toContain('The delivery charge cannot be negative.');
    expect(textContent(root)).toContain('1 to 365 days');
  });
});
