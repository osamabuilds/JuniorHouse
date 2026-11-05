/**
 * Small browser-only observers, each safe on the server and in old browsers (they do nothing there) and each returning a function that
 * stops it. Used by the product page's buy bar (BUY-2, 7, 9).
 */

/** Calls back with whether the element is in the viewport. Without IntersectionObserver the element counts as visible. */
export function watchInView(el: Element, onChange: (visible: boolean) => void): () => void {
  if (typeof IntersectionObserver === 'undefined') {
    onChange(true);
    return () => undefined;
  }
  const observer = new IntersectionObserver((entries) => {
    const last = entries[entries.length - 1];
    if (last) onChange(last.isIntersecting);
  });
  observer.observe(el);
  return () => observer.disconnect();
}

/** Calls back with the current result of a media query and every change. */
export function watchMedia(query: string, onChange: (matches: boolean) => void): () => void {
  if (typeof matchMedia === 'undefined') return () => undefined;
  const list = matchMedia(query);
  onChange(list.matches);
  const handler = (e: { matches: boolean }) => onChange(e.matches);
  list.addEventListener('change', handler);
  return () => list.removeEventListener('change', handler);
}

/** Calls back with the element's height now and whenever it changes. */
export function watchHeight(el: Element, onChange: (height: number) => void): () => void {
  onChange(el.getBoundingClientRect().height);
  if (typeof ResizeObserver === 'undefined') return () => undefined;
  const observer = new ResizeObserver(() => onChange(el.getBoundingClientRect().height));
  observer.observe(el);
  return () => observer.disconnect();
}
