import { TestBed } from '@angular/core/testing';
import { buttonNamed, choose, controlsWithoutLabels, setChecked, textContent, typeInto } from '@app/testing/dom-helpers';
import { FooterColumn } from '../../models';
import { FooterEditorComponent } from './footer-editor.component';

describe('FooterEditorComponent (spec S-3, ST-1, ST-4, ST-15)', () => {
  const columns: FooterColumn[] = [
    {
      title: 'Help',
      sort: 1,
      links: [
        { label: 'Delivery', target: { kind: 'Page', reference: 'delivery' }, sort: 1, enabled: true },
        { label: 'Contact', target: { kind: 'Page', reference: 'contact' }, sort: 2, enabled: true },
      ],
    },
    { title: 'Shop', sort: 2, links: [] },
  ];

  function create(errors: Record<string, string[]> = {}) {
    const fixture = TestBed.createComponent(FooterEditorComponent);
    fixture.componentRef.setInput('columns', columns);
    fixture.componentRef.setInput('errors', errors);
    const emitted: (readonly FooterColumn[])[] = [];
    fixture.componentInstance.columnsChange.subscribe((c) => emitted.push(c));
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('every control has a label (WCAG 1.3.1)', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('renames a column', () => {
    const { root, emitted } = create();

    typeInto(root, '#fc-title-1', 'Browse');

    expect(emitted[0][1].title).toBe('Browse');
    expect(emitted[0][0]).toEqual(columns[0]);
  });

  it('adds a link to a column with a path target to fill in', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Add a link to Shop').click();

    expect(emitted[0][1].links).toEqual([{ label: '', target: { kind: 'Path', reference: '/' }, sort: 1, enabled: true }]);
  });

  it('changes where a link goes with the typed target picker', () => {
    const { fixture, root, emitted } = create();

    choose(root, '#fl-0-1-kind', 'External');
    fixture.componentRef.setInput('columns', emitted[0]);
    fixture.detectChanges();
    typeInto(root, '#fl-0-1-reference', 'https://wa.me/923315613217');

    expect(emitted[0][0].links[1].target.kind).toBe('External');
    expect(emitted[1][0].links[1].target).toEqual({ kind: 'External', reference: 'https://wa.me/923315613217' });
  });

  it('switches a link off and edits its text', () => {
    const { root, emitted } = create();

    typeInto(root, '#fl-label-0-0', 'Shipping');
    setChecked(root, 'li.card--inner input[type=checkbox]', false);

    expect(emitted[0][0].links[0].label).toBe('Shipping');
    expect(emitted[1][0].links[0].enabled).toBe(false);
  });

  it('reorders links and columns with buttons and renumbers them (WCAG 2.5.7)', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Move the Contact link in Help up').click();
    buttonNamed(root, 'Move the Help column down').click();

    expect(emitted[0][0].links.map((l) => [l.label, l.sort])).toEqual([['Contact', 1], ['Delivery', 2]]);
    expect(emitted[1].map((c) => [c.title, c.sort])).toEqual([['Shop', 1], ['Help', 2]]);
  });

  it('removes a link or a whole column', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Remove the Delivery link in Help').click();
    buttonNamed(root, 'Remove the Shop column').click();

    expect(emitted[0][0].links.map((l) => l.label)).toEqual(['Contact']);
    expect(emitted[1].map((c) => c.title)).toEqual(['Help']);
  });

  it('shows server reasons under the column and under the link they belong to', () => {
    const { root } = create({ 'footer.Help': ['Enter a column title.'], 'footer.Help.Delivery': ['The page "delivery" does not exist or is not published.'] });

    expect(textContent(root)).toContain('Enter a column title.');
    expect(textContent(root)).toContain('does not exist or is not published');
  });
});
