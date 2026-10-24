import { canMove, moveItem, removeItem, replaceItem, withSort } from './list.util';

describe('list helpers (spec ST-2, ST-15)', () => {
  it('moves an item up or down by one place and leaves the ends alone', () => {
    expect(moveItem(['a', 'b', 'c'], 1, -1)).toEqual(['b', 'a', 'c']);
    expect(moveItem(['a', 'b', 'c'], 1, 1)).toEqual(['a', 'c', 'b']);
    expect(moveItem(['a', 'b', 'c'], 0, -1)).toEqual(['a', 'b', 'c']);
    expect(moveItem(['a', 'b', 'c'], 2, 1)).toEqual(['a', 'b', 'c']);
    expect(canMove(['a', 'b'], 0, -1)).toBe(false);
    expect(canMove(['a', 'b'], 0, 1)).toBe(true);
  });

  it('removes and replaces without touching the original', () => {
    const original = [{ n: 1 }, { n: 2 }];

    expect(removeItem(original, 0)).toEqual([{ n: 2 }]);
    expect(replaceItem(original, 1, { n: 9 })).toEqual([{ n: 1 }, { n: 9 }]);
    expect(original).toEqual([{ n: 1 }, { n: 2 }]);
  });

  it('numbers positions from 1 in the order shown', () => {
    expect(withSort([{ sort: 7 }, { sort: 3 }]).map((i) => i.sort)).toEqual([1, 2]);
  });
});
