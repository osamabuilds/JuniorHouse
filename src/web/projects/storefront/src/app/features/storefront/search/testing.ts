import { SuggestResult, SuggestionItem } from './models/search.model';

/** Suggestion data for specs: a few products and one category page. */
export const suggestionItems: readonly SuggestionItem[] = [
  { kind: 'category', label: 'Dresses · Girls', path: '/girls/dresses', image: null, price: null },
  {
    kind: 'product',
    label: 'Cotton dungaree',
    path: '/boys/jumpsuits/cotton-dungaree',
    image: { altText: 'Cotton dungaree', width: 1200, height: 1600, sources: [{ width: 320, webp: '/media/a-320.webp', jpeg: '/media/a-320.jpg' }] },
    price: 4100,
  },
  { kind: 'product', label: 'Floral summer dress', path: '/girls/dresses/floral-summer-dress', image: null, price: 3800 },
  { kind: 'product', label: 'Linen button shirt', path: '/boys/tops/linen-button-shirt', image: null, price: 3200 },
];

/** The answer a fake API gives: items whose label contains the text. */
export function fakeSuggest(query: string): SuggestResult {
  const q = query.toLowerCase();
  return { query, items: suggestionItems.filter((i) => i.label.toLowerCase().includes(q)) };
}
