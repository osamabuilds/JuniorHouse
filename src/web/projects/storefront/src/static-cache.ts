/** Cache lifetime for a built file: forever only when its name carries a content hash, otherwise the browser must revalidate. */
const HASHED_ASSET = /-[A-Za-z0-9_-]{8}\.(?:m?js|css|woff2?)$/;

export const IMMUTABLE = 'public, max-age=31536000, immutable';
export const REVALIDATE = 'no-cache';

/**
 * `main-KAW5WLCI.js` and `chunk-7nA6icML.js` change their name when their content changes, so they can be cached for a year.
 * `main.js` (a build without output hashing, such as the development container build), `index.csr.html`, `styles.css` and the
 * favicon keep their name: a long cache would keep serving the old bundle after a deploy, so they are revalidated (ETag) every time.
 */
export function cacheControlFor(filePath: string): string {
  return HASHED_ASSET.test(filePath) ? IMMUTABLE : REVALIDATE;
}
