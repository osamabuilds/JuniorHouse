import { TestBed } from '@angular/core/testing';
import { choose, controlsWithoutLabels, textContent, typeInto } from '@app/testing/dom-helpers';
import { Target } from '../../models';
import { LinkTargetPickerComponent } from './link-target-picker.component';

describe('LinkTargetPickerComponent (spec ST-1, ST-4)', () => {
  function create(target: Target | null, errors: string[] = []) {
    const fixture = TestBed.createComponent(LinkTargetPickerComponent);
    fixture.componentRef.setInput('target', target);
    fixture.componentRef.setInput('idPrefix', 'tp');
    fixture.componentRef.setInput('label', 'Delivery link');
    fixture.componentRef.setInput('errors', errors);
    const changed: Target[] = [];
    fixture.componentInstance.changed.subscribe((t) => changed.push(t));
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, changed };
  }

  it('labels both controls, naming the link they belong to (WCAG 1.3.1)', () => {
    const { root } = create({ kind: 'Path', reference: '/girls' });

    expect(controlsWithoutLabels(root)).toEqual([]);
    expect(textContent(root)).toContain('Delivery link goes to');
  });

  it('offers a path, a page, a collection or another website, but not a free web address', () => {
    const { root } = create({ kind: 'Path', reference: '/' });

    const options = Array.from(root.querySelectorAll('#tp-kind option')).map((o) => (o as HTMLOptionElement).value);
    expect(options).toEqual(['Path', 'Page', 'Collection', 'External']);
  });

  it('changing what it points at keeps the reference typed so far', () => {
    const { root, changed } = create({ kind: 'Path', reference: 'delivery' });

    choose(root, '#tp-kind', 'Page');

    expect(changed).toEqual([{ kind: 'Page', reference: 'delivery' }]);
  });

  it('trims what is typed and reports it with the kind', () => {
    const { root, changed } = create({ kind: 'Page', reference: '' });

    typeInto(root, '#tp-reference', '  size-guide ');

    expect(changed).toEqual([{ kind: 'Page', reference: 'size-guide' }]);
  });

  it('tells the user what to type for each kind', () => {
    expect(textContent(create({ kind: 'Path', reference: '' }).root)).toContain('/girls/dresses');
    expect(textContent(create({ kind: 'External', reference: '' }).root)).toContain('https://');
    expect(textContent(create({ kind: 'Collection', reference: '' }).root)).toContain('collection code');
  });

  it('shows a kind that is no longer offered rather than silently replacing it', () => {
    const { root } = create({ kind: 'Category', reference: 'dresses' });

    expect((root.querySelector('#tp-kind') as HTMLSelectElement).value).toBe('Category');
  });

  it('shows the server reason under the reference', () => {
    const { root } = create({ kind: 'Page', reference: 'nope' }, ['The page "nope" does not exist or is not published.']);

    expect(textContent(root)).toContain('does not exist or is not published');
  });
});
