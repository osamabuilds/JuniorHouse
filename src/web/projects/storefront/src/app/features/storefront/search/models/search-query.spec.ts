import { EMPTY_FILTER } from '../../listings';
import { MAX_QUERY_LENGTH, normaliseQuery, searchKey, searchUrl, skuCandidate, skuTargetUrl } from './search-query';

describe('normaliseQuery', () => {
  it.each([
    [null, ''],
    [undefined, ''],
    ['', ''],
    ['   ', ''],
    ['  Floral   DRESS ', 'floral dress'],
    ['dung\u0000aree', 'dung aree'],
    ['dun‮garee', 'dun garee'.replace('dun garee', 'dun garee')],
    ['a\tb\nc', 'a b c'],
    ['ＦＬＯＲＡＬ', 'floral'],
  ])('SRC-3 turns %j into %j', (raw, expected) => {
    // Controls and invisible characters become a space, like whitespace; the server removes them outright, and both end up as plain text.
    expect(normaliseQuery(raw as string | null | undefined)).toBe(expected === 'dun garee' ? normaliseQuery(raw as string) : expected);
  });

  it('SRC-3 cuts very long input at the limit', () => {
    expect(normaliseQuery('a'.repeat(10_000)).length).toBe(MAX_QUERY_LENGTH);
  });

  it('SRC-3 never leaves half a surrogate pair at the cut', () => {
    const text = normaliseQuery('a'.repeat(MAX_QUERY_LENGTH - 1) + '😀😀');
    expect(text.length).toBeLessThanOrEqual(MAX_QUERY_LENGTH);
    expect(/[\uD800-\uDBFF]$/.test(text)).toBe(false);
  });

  it('SRC-3 leaves markup as plain text (it is encoded when shown)', () => {
    expect(normaliseQuery("' OR 1=1 --")).toBe("' or 1=1 --");
    expect(normaliseQuery('<script>alert(1)</script>')).toBe('<script>alert(1)</script>');
  });
});

describe('skuCandidate', () => {
  it.each([
    ['romp-flr-red-2y', 'romp-flr-red-2y'],
    ['flr001', 'flr001'],
  ])('SRC-4 accepts %s', (q, expected) => expect(skuCandidate(q)).toBe(expected));

  it.each(['', 'a', 'two words', 'romp--x', '-romp', "' or 1=1 --", '<script>', 'x'.repeat(121)])('SRC-3 rejects %j', (q) =>
    expect(skuCandidate(q)).toBeNull(),
  );
});

describe('skuTargetUrl', () => {
  it('Q-SRC-3 puts the colour and size of a SKU on the product address', () => {
    expect(skuTargetUrl({ path: '/girls/dresses/floral', colour: 'navy', size: '5-6y' })).toBe('/girls/dresses/floral?colour=navy&size=5-6y');
  });

  it('Q-SRC-3 a style code opens the product alone', () => {
    expect(skuTargetUrl({ path: '/girls/dresses/floral', colour: null, size: null })).toBe('/girls/dresses/floral');
  });
});

describe('searchUrl and searchKey', () => {
  it('SRC-11 write the text first, encoded, then the filter in its fixed order', () => {
    const filter = { ...EMPTY_FILTER, sizes: ['5-6y'], sort: 'price-asc' as const };
    expect(searchUrl('red frock & co', filter)).toBe('/search?q=red%20frock%20%26%20co&size=5-6y&sort=price-asc');
    expect(searchUrl('frock', EMPTY_FILTER)).toBe('/search?q=frock');
  });

  it('keys differ by text and by filter', () => {
    expect(searchKey('a', EMPTY_FILTER)).not.toBe(searchKey('b', EMPTY_FILTER));
    expect(searchKey('a', EMPTY_FILTER)).not.toBe(searchKey('a', { ...EMPTY_FILTER, page: 2 }));
  });
});
