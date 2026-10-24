import { TestBed } from '@angular/core/testing';
import { buttonNamed, controlsWithoutLabels, hasButton, setChecked, textContent, typeInto } from '@app/testing/dom-helpers';
import { AgeStep } from '../../models';
import { AgesEditorComponent } from './ages-editor.component';

describe('AgesEditorComponent (spec S-2, ST-2, ST-15)', () => {
  const steps: AgeStep[] = [
    { code: '1y', label: '1Y', sort: 1, enabled: true },
    { code: '2y', label: '2Y', sort: 2, enabled: true },
    { code: '', label: '', sort: 3, enabled: true },
  ];

  function create(errors: Record<string, string[]> = {}) {
    const fixture = TestBed.createComponent(AgesEditorComponent);
    fixture.componentRef.setInput('steps', steps);
    fixture.componentRef.setInput('lockedCodes', ['1y', '2y']);
    fixture.componentRef.setInput('errors', errors);
    const emitted: (readonly AgeStep[])[] = [];
    fixture.componentInstance.stepsChange.subscribe((s) => emitted.push(s));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('every control has a label (WCAG 1.3.1)', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('a code that was saved is read-only, because links use it; a new one can be typed (ST-2)', () => {
    const { root } = create();

    expect((root.querySelector('#age-code-0') as HTMLInputElement).readOnly).toBe(true);
    expect((root.querySelector('#age-code-2') as HTMLInputElement).readOnly).toBe(false);
    expect(textContent(root)).toContain('Used in links, so it cannot change.');
  });

  it('renaming a label changes only the label (ST-2)', () => {
    const { root, emitted } = create();

    typeInto(root, '#age-label-0', '1 year');

    expect(emitted[0][0]).toEqual({ code: '1y', label: '1 year', sort: 1, enabled: true });
    expect(emitted[0][1]).toEqual(steps[1]);
  });

  it('a typed code is lower-cased and trimmed', () => {
    const { root, emitted } = create();

    typeInto(root, '#age-code-2', ' 3Y ');

    expect(emitted[0][2].code).toBe('3y');
  });

  it('switches an age off with a checkbox', () => {
    const { root, emitted } = create();

    setChecked(root, '.form-field input[type=checkbox]', false);

    expect(emitted).toHaveLength(1);
    expect(emitted[0].filter((s) => !s.enabled)).toHaveLength(1);
  });

  it('reorders with buttons and renumbers the positions (WCAG 2.5.7)', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Move the 1Y age down').click();

    expect(emitted[0].map((s) => s.code)).toEqual(['2y', '1y', '']);
    expect(emitted[0].map((s) => s.sort)).toEqual([1, 2, 3]);
  });

  it('a saved age cannot be removed, a new one can (ST-2)', () => {
    const { root, emitted } = create();

    expect(hasButton(root, 'Remove the 1Y age')).toBe(false);
    expect(textContent(root)).toContain('A published age cannot be removed');
    buttonNamed(root, 'Remove the new age').click();

    expect(emitted[0].map((s) => s.code)).toEqual(['1y', '2y']);
  });

  it('shows the server reason under the age it belongs to', () => {
    const { root } = create({ 'ages.2y': ['The age code "2y" is used twice.'] });

    expect(textContent(root)).toContain('The age code "2y" is used twice.');
  });

});
