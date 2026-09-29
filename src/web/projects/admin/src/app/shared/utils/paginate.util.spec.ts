import { paginate } from './paginate.util';

describe('paginate', () => {
  const items = Array.from({ length: 25 }, (_, i) => i + 1);

  it('returns the requested slice', () => {
    expect(paginate(items, 2, 10)).toEqual({ items: [11, 12, 13, 14, 15, 16, 17, 18, 19, 20], page: 2 });
  });

  it('returns the short last page', () => {
    expect(paginate(items, 3, 10).items).toEqual([21, 22, 23, 24, 25]);
  });

  it('clamps a page that no longer exists after the list shrinks', () => {
    expect(paginate(items.slice(0, 5), 4, 10)).toEqual({ items: [1, 2, 3, 4, 5], page: 1 });
  });

  it('handles an empty list', () => {
    expect(paginate([], 1, 10)).toEqual({ items: [], page: 1 });
  });
});
