import { EMPTY_FILTER, listingKey, parseFilter, scopePath, toApiQuery, toQueryString } from './listing-query';

describe('listing query', () => {
  it('LST-11 an empty query is the default filter', () => {
    expect(parseFilter({})).toEqual(EMPTY_FILTER);
  });

  it('LST-11 hostile values are dropped, never kept', () => {
    const f = parseFilter({ size: ['5-6y', '<script>alert(1)</script>', '../x'], colour: 'NAVY', price: '1e309-5', sort: "'; drop", page: '-3', age: '<b>' });
    expect(f.sizes).toEqual(['5-6y']);
    expect(f.colours).toEqual(['navy']);
    expect([f.priceMin, f.priceMax, f.sort, f.page, f.age]).toEqual([null, null, 'recent', 1, null]);
  });

  it('LST-11 thousands of values are capped at twenty', () => {
    const many = Array.from({ length: 5000 }, (_, i) => `s${i}`);
    expect(parseFilter({ size: many }).sizes.length).toBe(20);
  });

  it('LST-2 a price range reads both ends, either optional', () => {
    expect(parseFilter({ price: '100-300' })).toMatchObject({ priceMin: 100, priceMax: 300 });
    expect(parseFilter({ price: '100-' })).toMatchObject({ priceMin: 100, priceMax: null });
    expect(parseFilter({ price: '-300' })).toMatchObject({ priceMin: null, priceMax: 300 });
    expect(parseFilter({ price: '300-100' })).toMatchObject({ priceMin: null, priceMax: null });
  });

  it('LST-10 one filter has one spelling whatever the order of the input', () => {
    const a = parseFilter({ sort: 'price-asc', size: ['7-8y', '5-6y'], age: 'KIDS', page: '2' });
    const b = parseFilter({ page: '2', age: 'kids', size: ['5-6y', '7-8y', '5-6y'], sort: 'price-asc' });
    expect(toQueryString(a)).toBe('age=kids&size=5-6y&size=7-8y&sort=price-asc&page=2');
    expect(toQueryString(b)).toBe(toQueryString(a));
    expect(toQueryString(a, false)).not.toContain('page');
  });

  it('LST-16 the API gets the scope and the canonical filter', () => {
    expect(toApiQuery({ kind: 'category', gender: 'girls', category: 'dresses' }, parseFilter({ age: 'age-2y' }))).toEqual({
      scope: 'category', age: 'age-2y', gender: 'girls', category: 'dresses',
    });
  });

  it('LST-4 the key changes with the page and with the filter', () => {
    const scope = { kind: 'audience', gender: 'girls' } as const;
    expect(listingKey(scope, parseFilter({ page: '2' }))).not.toBe(listingKey(scope, parseFilter({})));
    expect(listingKey(scope, parseFilter({ size: 'a' }))).not.toBe(listingKey(scope, parseFilter({})));
  });

  it('NAV-8 scopes map to their clean paths', () => {
    expect(scopePath({ kind: 'audience', gender: 'girls' })).toBe('/girls');
    expect(scopePath({ kind: 'category', gender: 'girls', category: 'dresses' })).toBe('/girls/dresses');
    expect(scopePath({ kind: 'new-in', gender: 'girls' })).toBe('/girls/new-in');
    expect(scopePath({ kind: 'sale' })).toBe('/sale');
    expect(scopePath({ kind: 'collection', collection: 'summer' })).toBe('/collections/summer');
  });
});
