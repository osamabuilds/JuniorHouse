import { NavItem } from '../models';
import {
  addMember,
  addRoot,
  applyNavAction,
  canAddMember,
  canIndent,
  canMoveDown,
  canMoveUp,
  canOutdent,
  fieldNameAt,
  flatten,
  indent,
  isValidKey,
  moveDown,
  moveUp,
  newEntry,
  outdent,
  removeAt,
  slugify,
  uniqueKey,
  updateAt,
} from './nav-tree.util';

const entry = (key: string, children: NavItem[] = []): NavItem => ({
  key, label: key.toUpperCase(), kind: 'Link', target: { kind: 'Path', reference: `/${key}` }, sort: 0, enabled: true, showAges: false, children,
});

const keys = (items: readonly NavItem[]): string[] => items.map((i) => i.key);

describe('navigation tree helpers (spec ST-1, ST-1a, ST-15)', () => {
  const tree = () => addRoot(addRoot(addRoot([], entry('girls', [entry('dresses'), entry('tops', [entry('tees')])])), entry('boys')), entry('sale'));

  it('numbers positions from 1 in the order shown', () => {
    const items = tree();

    expect(items.map((i) => i.sort)).toEqual([1, 2, 3]);
    expect(items[0].children.map((i) => i.sort)).toEqual([1, 2]);
  });

  it('moves an entry up and down among its siblings with plain buttons (WCAG 2.5.7)', () => {
    const down = moveDown(tree(), [0]);
    expect(keys(down)).toEqual(['boys', 'girls', 'sale']);
    expect(down.map((i) => i.sort)).toEqual([1, 2, 3]);

    expect(keys(moveUp(down, [1]))).toEqual(['girls', 'boys', 'sale']);
    expect(keys(moveUp(tree(), [0, 1]).find((i) => i.key === 'girls')!.children)).toEqual(['tops', 'dresses']);
  });

  it('cannot move the first entry up or the last one down', () => {
    expect(canMoveUp([0])).toBe(false);
    expect(canMoveDown(tree(), [2])).toBe(false);
    expect(keys(moveUp(tree(), [0]))).toEqual(['girls', 'boys', 'sale']);
    expect(keys(moveDown(tree(), [2]))).toEqual(['girls', 'boys', 'sale']);
  });

  it('indent makes an entry the last member of the one above it', () => {
    const items = indent(tree(), [1]); // boys under girls

    expect(keys(items)).toEqual(['girls', 'sale']);
    expect(keys(items[0].children)).toEqual(['dresses', 'tops', 'boys']);
  });

  it('indent is refused for the first entry and when the result would be a fourth level (ST-1a)', () => {
    expect(canIndent(tree(), [0])).toBe(false);
    // "tops" has a member; indenting it under "dresses" would put "tees" on level 4.
    expect(canIndent(tree(), [0, 1])).toBe(false);
    expect(canIndent(tree(), [1])).toBe(true);
  });

  it('outdent makes an entry a sibling placed right after its parent', () => {
    const items = outdent(tree(), [0, 0]); // dresses out of girls

    expect(keys(items)).toEqual(['girls', 'dresses', 'boys', 'sale']);
    expect(keys(items[0].children)).toEqual(['tops']);
    expect(canOutdent([0])).toBe(false);
  });

  it('adds a member under an entry, but not below the third level (ST-1a)', () => {
    const items = addMember(tree(), [1], entry('polos'));

    expect(keys(items[1].children)).toEqual(['polos']);
    expect(canAddMember([0, 1, 0])).toBe(false);
    expect(addMember(tree(), [0, 1, 0], entry('x'))).toEqual(tree());
  });

  it('removes an entry with everything under it and renumbers', () => {
    const items = removeAt(tree(), [0]);

    expect(keys(items)).toEqual(['boys', 'sale']);
    expect(items.map((i) => i.sort)).toEqual([1, 2]);
  });

  it('changes one entry without touching the others', () => {
    const items = updateAt(tree(), [0, 1], { label: 'Tops and tees', enabled: false });

    expect(items[0].children[1]).toMatchObject({ label: 'Tops and tees', enabled: false, key: 'tops' });
    expect(items[0].children[0].enabled).toBe(true);
  });

  it('names an entry the way the server does in a field error', () => {
    expect(fieldNameAt(tree(), [0, 1, 0])).toBe('navigation.girls.tops.tees');
    expect(fieldNameAt(tree(), [2])).toBe('navigation.sale');
  });

  it('builds a key from the label that no other entry uses', () => {
    const items = tree();

    expect(slugify('Tops & Tees!')).toBe('tops-tees');
    expect(uniqueKey(items, 'Sale')).toBe('sale-2');
    expect(uniqueKey(items, 'New in')).toBe('new-in');
    expect(isValidKey(uniqueKey(items, '?'))).toBe(true);
    expect(newEntry(items, 'Gifts')).toMatchObject({ key: 'gifts', label: 'Gifts', enabled: true, children: [] });
  });

  it('flattens the whole tree', () => {
    expect(keys(flatten(tree()))).toEqual(['girls', 'dresses', 'tops', 'tees', 'boys', 'sale']);
  });

  it('never mutates the tree it was given', () => {
    const original = tree();
    const snapshot = JSON.stringify(original);

    moveDown(original, [0]);
    indent(original, [1]);
    outdent(original, [0, 0]);
    removeAt(original, [0]);
    updateAt(original, [0], { label: 'x' });

    expect(JSON.stringify(original)).toBe(snapshot);
  });
});

describe('applyNavAction (spec ST-1, ST-1a)', () => {
  const items = addRoot(addRoot([], entry('girls')), entry('boys'));

  it('adds a member with a fresh key under the chosen entry', () => {
    const result = applyNavAction(items, { type: 'add-member', path: [0] });

    expect(result[0].children).toHaveLength(1);
    expect(result[0].children[0]).toMatchObject({ label: 'New entry', key: 'new-entry', enabled: true });
  });

  it('gives a second new entry a different key', () => {
    const once = applyNavAction(items, { type: 'add-member', path: [0] });
    const twice = applyNavAction(once, { type: 'add-member', path: [1] });

    expect(twice[1].children[0].key).toBe('new-entry-2');
  });

  it('applies updates, moves and removals', () => {
    expect(applyNavAction(items, { type: 'update', path: [1], patch: { label: 'Lads' } })[1].label).toBe('Lads');
    expect(keys(applyNavAction(items, { type: 'move', path: [1], direction: 'up' }))).toEqual(['boys', 'girls']);
    expect(keys(applyNavAction(items, { type: 'move', path: [1], direction: 'indent' }))).toEqual(['girls']);
    expect(keys(applyNavAction(items, { type: 'remove', path: [0] }))).toEqual(['boys']);
  });

  it('will not add a member to an entry on the third level (ST-1a)', () => {
    const deep = addMember(addMember(items, [0], entry('a')), [0, 0], entry('b'));

    expect(applyNavAction(deep, { type: 'add-member', path: [0, 0, 0] })).toEqual(deep);
  });
});
