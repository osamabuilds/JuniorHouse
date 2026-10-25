/**
 * The colour and size a visitor asked for, read from the address. They travel as `?colour=navy&size=5-6y` so the choice works without
 * scripting and can be shared; the page canonical stays the product itself (Q-PDP-2, SEO-06). Anything that is not a plausible code is
 * dropped here, and the server ignores a code the product does not have (PDP-2, 3). Pure.
 */

export interface ProductSelection {
  readonly colour: string | null;
  readonly size: string | null;
}

const CODE = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const MAX_LENGTH = 40;

function code(value: string | null | undefined): string | null {
  const v = (value ?? '').trim().toLowerCase();
  return v.length > 0 && v.length <= MAX_LENGTH && CODE.test(v) ? v : null;
}

export function parseSelection(raw: { readonly colour?: string | null; readonly size?: string | null }): ProductSelection {
  return { colour: code(raw.colour), size: code(raw.size) };
}

/** The query parameters for a selection, in one fixed order, with nothing for what is not chosen. */
export function selectionQuery(selection: ProductSelection): Record<string, string> {
  return {
    ...(selection.colour ? { colour: selection.colour } : {}),
    ...(selection.size ? { size: selection.size } : {}),
  };
}

export function selectionQueryString(selection: ProductSelection): string {
  return Object.entries(selectionQuery(selection)).map(([k, v]) => `${k}=${v}`).join('&');
}

/** One key for one request, so a result for an older choice is never shown for a newer one. */
export function productKey(path: string, selection: ProductSelection): string {
  return `${path}?${selectionQueryString(selection)}`;
}
