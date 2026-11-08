import { IMMUTABLE, REVALIDATE, cacheControlFor } from './static-cache';

describe('cacheControlFor', () => {
  it('caches content-hashed bundles for a year (production build)', () => {
    expect(cacheControlFor('/app/dist/storefront/browser/main-KAW5WLCI.js')).toBe(IMMUTABLE);
    expect(cacheControlFor('/app/dist/storefront/browser/chunk-7nA6icML.js')).toBe(IMMUTABLE);
    expect(cacheControlFor('/app/dist/storefront/browser/styles-7VC27FZX.css')).toBe(IMMUTABLE);
  });

  it('makes the unhashed main.js revalidate, so a deploy is picked up (development container build)', () => {
    expect(cacheControlFor('/app/dist/storefront/browser/main.js')).toBe(REVALIDATE);
    expect(cacheControlFor('C:\app\dist\storefront\browser\main.js')).toBe(REVALIDATE);
  });

  it('makes the page shell, unhashed styles and the favicon revalidate', () => {
    expect(cacheControlFor('/browser/index.csr.html')).toBe(REVALIDATE);
    expect(cacheControlFor('/browser/styles.css')).toBe(REVALIDATE);
    expect(cacheControlFor('/browser/favicon.ico')).toBe(REVALIDATE);
  });

  it('does not treat a short or unrelated suffix as a hash', () => {
    expect(cacheControlFor('/browser/polyfills-1.js')).toBe(REVALIDATE);
    expect(cacheControlFor('/browser/main-es.js')).toBe(REVALIDATE);
  });
});
