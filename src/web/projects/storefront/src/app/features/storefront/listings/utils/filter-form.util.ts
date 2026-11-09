import { ListingFilter, parseFilter } from '../models/listing-query';

/**
 * The filter a filter form currently describes, read the same way the page reads a query (so a hostile value is dropped, one filter has one
 * spelling) and always from page 1. Used both when the form is submitted and while the phone sheet is open, for its live count.
 */
export function filterFromForm(form: HTMLFormElement): ListingFilter {
  const raw: Record<string, string[]> = {};
  for (const [k, v] of new FormData(form).entries()) if (typeof v === 'string' && v !== '') (raw[k] ??= []).push(v);
  if (raw['priceMin'] || raw['priceMax']) raw['price'] = [`${raw['priceMin']?.[0] ?? ''}-${raw['priceMax']?.[0] ?? ''}`];
  return { ...parseFilter(raw), page: 1 };
}
