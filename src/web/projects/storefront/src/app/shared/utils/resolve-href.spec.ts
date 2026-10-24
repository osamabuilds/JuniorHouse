import { resolveHref } from './resolve-href';

describe('resolveHref', () => {
  it('SC-3 builds clean URLs for page, path, category and collection targets', () => {
    expect(resolveHref({ kind: 'page', slug: 'delivery' })).toBe('/delivery');
    expect(resolveHref({ kind: 'path', path: '/boys/bottoms' })).toBe('/boys/bottoms');
    expect(resolveHref({ kind: 'category', code: 'tees', path: '/boys/tees-and-shirts' })).toBe('/boys/tees-and-shirts');
    expect(resolveHref({ kind: 'collection', code: 'summer-2027' })).toBe('/collections/summer-2027');
  });

  it('SC-3 accepts an https external URL only', () => {
    expect(resolveHref({ kind: 'external', url: 'https://wa.me/923315613217' })).toBe('https://wa.me/923315613217');
    expect(resolveHref({ kind: 'external', url: 'http://example.com' })).toBeNull();
    expect(resolveHref({ kind: 'external', url: 'javascript:alert(1)' })).toBeNull();
  });

  it('SC-3 refuses unsafe or malformed internal values', () => {
    expect(resolveHref({ kind: 'path', path: '//evil.example' })).toBeNull();
    expect(resolveHref({ kind: 'path', path: 'boys' })).toBeNull();
    expect(resolveHref({ kind: 'path', path: '/a b' })).toBeNull();
    expect(resolveHref({ kind: 'page', slug: '../x' })).toBeNull();
    expect(resolveHref({ kind: 'collection', code: 'A/B' })).toBeNull();
  });
});
