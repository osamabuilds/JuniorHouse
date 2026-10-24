import { TestBed } from '@angular/core/testing';
import { navItem } from '@app/testing/storefront-fixtures';
import { NavAction, NavItem } from '../../models';
import { NavNodeComponent } from './nav-node.component';

describe('NavNodeComponent (spec ST-1, ST-1a, ST-15)', () => {
  const tree: NavItem[] = [
    navItem({ key: 'girls', label: 'Girls', children: [navItem({ key: 'dresses', label: 'Dresses', sort: 1, children: [navItem({ key: 'frocks', label: 'Frocks' })] })] }),
    navItem({ key: 'boys', label: 'Boys', sort: 2 }),
  ];

  function create(path: number[], fieldErrors: Record<string, string[]> = {}) {
    const item = path.reduce<NavItem | undefined>((found, index) => (found ? found.children[index] : tree[index]), undefined) as NavItem;
    const fixture = TestBed.createComponent(NavNodeComponent);
    fixture.componentRef.setInput('item', item);
    fixture.componentRef.setInput('path', path);
    fixture.componentRef.setInput('root', tree);
    fixture.componentRef.setInput('fieldErrors', fieldErrors);
    const actions: NavAction[] = [];
    fixture.componentInstance.action.subscribe((a) => actions.push(a));
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const button = (name: RegExp): HTMLButtonElement =>
      Array.from(root.querySelectorAll('button')).find((b) => name.test(b.getAttribute('aria-label') ?? '')) as HTMLButtonElement;
    return { fixture, root, actions, button };
  }

  it('gives every control a label and every action a name that says which entry it is for (WCAG 2.2, NFR-19)', () => {
    const { root } = create([0]);

    for (const control of Array.from(root.querySelectorAll('input:not([type=checkbox]), select'))) {
      const id = control.getAttribute('id');
      expect(root.querySelector(`label[for="${id}"]`), `label for ${id}`).not.toBeNull();
    }

    const names = Array.from((root.querySelector('.nav-node__actions') as HTMLElement).querySelectorAll('button')).map((b) => b.getAttribute('aria-label'));
    expect(names.every((n) => n !== null && n.includes('Girls'))).toBe(true);
  });

  it('reorders with plain buttons, no dragging (WCAG 2.5.7)', () => {
    const { actions, button } = create([0]);

    button(/Move Girls down/).click();

    expect(actions).toEqual([{ type: 'move', path: [0], direction: 'down' }]);
  });

  it('cannot move the first entry up or the last one down, and the buttons say so by being disabled', () => {
    expect(create([0]).button(/Move Girls up/).disabled).toBe(true);
    expect(create([1]).button(/Move Boys down/).disabled).toBe(true);
    expect(create([1]).button(/Move Boys up/).disabled).toBe(false);
  });

  it('offers indent and outdent as buttons, each only where it can work', () => {
    expect(create([1]).button(/Make Boys a member/).disabled).toBe(false);
    expect(create([0]).button(/Make Girls a member/).disabled).toBe(true);
    expect(create([0]).button(/Move Girls out/).disabled).toBe(true);
    expect(create([0, 0]).button(/Move Dresses out/).disabled).toBe(false);
  });

  it('adds a member only above the third level, and explains why not (ST-1a)', () => {
    const top = create([0]);
    top.button(/Add a member to Girls/).click();
    expect(top.actions).toEqual([{ type: 'add-member', path: [0] }]);

    const third = create([0, 0, 0]);
    expect(third.button(/Add a member to Frocks/).disabled).toBe(true);
    expect(third.root.textContent).toContain('Three levels is the most');
  });

  it('reports an edit as an action on this entry, not a change to the tree', () => {
    const { root, actions } = create([1]);
    const label = root.querySelector('#nn-1-label') as HTMLInputElement;

    label.value = 'Lads';
    label.dispatchEvent(new Event('input'));

    expect(actions).toEqual([{ type: 'update', path: [1], patch: { label: 'Lads' } }]);
  });

  it('a group has no target to fill in', () => {
    const { root, actions, fixture } = create([1]);
    const kind = root.querySelector('#nn-1-kind') as HTMLSelectElement;

    kind.value = 'Group';
    kind.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(actions[0]).toEqual({ type: 'update', path: [1], patch: { kind: 'Group', target: null } });
  });

  it('shows the server reason under the entry it belongs to, found by its key path (ST-1a, ST-4)', () => {
    const { root } = create([0, 0], { 'navigation.girls.dresses': ['"Dresses" is a group with nothing in it.'], 'navigation.boys': ['not here'] });

    expect(root.textContent).toContain('"Dresses" is a group with nothing in it.');
    expect(root.textContent).not.toContain('not here');
  });

  it('shows a hidden entry as hidden, in text and not only in colour', () => {
    tree[1] = navItem({ key: 'boys', label: 'Boys', sort: 2, enabled: false });
    const { root } = create([1]);

    expect(root.querySelector('.badge')?.textContent?.trim()).toBe('hidden');
    tree[1] = navItem({ key: 'boys', label: 'Boys', sort: 2 });
  });
});
