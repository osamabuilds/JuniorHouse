import { TestBed } from '@angular/core/testing';
import { draftSettings } from '@app/testing/storefront-fixtures';
import { buttonNamed, controlsWithoutLabels, setChecked, textContent, typeInto } from '@app/testing/dom-helpers';
import { ContactValues, toContactValues } from '../../utils/contact.util';
import { ContactFormComponent } from './contact-form.component';

describe('ContactFormComponent (spec S-3, ST-4, ST-15)', () => {
  function create(values: ContactValues = toContactValues(draftSettings()), errors: Record<string, string[]> = {}) {
    const fixture = TestBed.createComponent(ContactFormComponent);
    fixture.componentRef.setInput('values', values);
    fixture.componentRef.setInput('errors', errors);
    const emitted: ContactValues[] = [];
    fixture.componentInstance.valuesChange.subscribe((v) => emitted.push(v));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('every field has a visible label (WCAG 1.3.1)', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('shows the saved values, and the format the phone number must have (ST-4)', () => {
    const { root } = create();

    expect((root.querySelector('#cf-name') as HTMLInputElement).value).toBe('Romp');
    expect((root.querySelector('#cf-whatsapp') as HTMLInputElement).value).toBe('+923315613217');
    expect(textContent(root)).toContain('for example +923001234567');
  });

  it('reports an edited field and leaves the others as they were', () => {
    const { root, emitted } = create();

    typeInto(root, '#cf-email', 'hello@example.com');

    expect(emitted[0]).toMatchObject({ email: 'hello@example.com', brandName: 'Romp' });
  });

  it('switches the announcement bar on with a checkbox', () => {
    const { root, emitted } = create();

    setChecked(root, 'section[aria-labelledby="cf-announce"] input[type=checkbox]', true);

    expect(emitted[0].announcementOn).toBe(true);
  });

  it('adds, edits, reorders and removes popular searches with buttons', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Add a search term').click();
    expect(emitted[0].popularSearches).toEqual(['Dresses', 'Shorts', '']);

    typeInto(root, '#cf-search-0', 'Frocks');
    expect(emitted[1].popularSearches).toEqual(['Frocks', 'Shorts']);

    buttonNamed(root, 'Move the search term Shorts up').click();
    expect(emitted[2].popularSearches).toEqual(['Shorts', 'Dresses']);

    buttonNamed(root, 'Remove the search term Dresses').click();
    expect(emitted[3].popularSearches).toEqual(['Shorts']);
  });

  it('stops adding at twelve searches and says why', () => {
    const many = { ...toContactValues(draftSettings()), popularSearches: Array.from({ length: 12 }, (_, i) => `term ${i}`) };
    const { root } = create(many);

    expect(buttonNamed(root, 'Add a search term').disabled).toBe(true);
    expect(textContent(root)).toContain('Up to 12');
  });

  it('shows each server reason under its own field (ST-4)', () => {
    const { root } = create(undefined, {
      'settings.Whatsapp': ['Enter the number with the country code and no spaces, for example +923001234567.'],
      'settings.Email': ['Enter a valid email address, for example hello@example.com.'],
      popularSearches: ['"Dresses" is listed twice.'],
    });

    expect(textContent(root)).toContain('Enter a valid email address');
    expect(textContent(root)).toContain('"Dresses" is listed twice.');
    expect(root.querySelectorAll('[role="alert"]').length).toBe(3);
  });
});
