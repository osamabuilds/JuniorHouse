import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { ListingTile } from '../../../listings';
import { provideSiteSettings } from '../../../site-settings';
import { SearchPageData, SearchResult } from '../../models/search.model';
import { SearchApi } from '../../services/search-api.service';
import { provideSearch } from '../../search.providers';
import { SearchPageContainer } from './search-page.container';

const tile = (n: number): ListingTile => ({
  path: `/girls/dresses/dress-${n}`,
  name: `Dress ${n}`,
  price: 800,
  compareAtPrice: null,
  savingPercent: 0,
  savingAmount: 0,
  colourCount: 1,
  hasSoldOutSizes: false,
  sizes: [],
  image: { altText: `Dress ${n}`, width: 800, height: 1000, sources: [{ width: 320, webp: '/media/h-320.webp', jpeg: '/media/h-320.jpg' }] },
});

const data = (tiles: ListingTile[], over: Partial<SearchPageData> = {}): SearchPageData => ({
  query: 'frock',
  tiles,
  total: tiles.length,
  page: 1,
  pageSize: 24,
  totalPages: 1,
  maxSavingPercent: 0,
  options: { sizes: [{ value: '5-6y', label: '5-6 Years' }], colours: [{ value: 'red', label: 'Red' }], priceMin: 500, priceMax: 900, ages: [] },
  ...over,
});

const ok = (tiles: ListingTile[], over: Partial<SearchPageData> = {}): SearchResult => ({
  status: 'ok',
  page: data(tiles, over),
  suggestions: tiles.length === 0 ? [{ label: 'New in', path: '/new-in' }] : [],
});

function render(result: SearchResult | null, query: Record<string, string | string[]> = { q: 'frock' }, responseInit?: ResponseInit) {
  const find = vi.fn(() => of(result as SearchResult));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      provideSearch(),
      { provide: SearchApi, useValue: { find, suggest: () => of({ query: '', items: [] }) } },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
      { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap(query)), snapshot: {} } },
    ],
  });
  const fixture = TestBed.createComponent(SearchPageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  return { el: fixture.nativeElement as HTMLElement, find };
}

describe('SearchPageContainer', () => {
  it('SRC-1 shows one h1 with the encoded text, a search form and the tiles', () => {
    const { el, find } = render(ok([tile(1), tile(2)]));

    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Results for “frock”');
    expect(el.querySelector('form[role="search"]')!.getAttribute('action')).toBe('/search');
    expect(el.querySelector<HTMLInputElement>('form[role="search"] input[name="q"]')!.value).toBe('frock');
    expect(el.querySelectorAll('ul.rp-grid > li > rp-product-tile article').length).toBe(2);
    expect(find).toHaveBeenCalledTimes(1);
  });

  it('SRC-3 typed markup is shown as text and never becomes elements', () => {
    const { el } = render(ok([]), { q: '<img src=x onerror=alert(1)>' });

    expect(el.querySelector('img[src="x"]')).toBeNull();
    expect(el.querySelector('h1')!.textContent).toContain('<img src=x onerror=alert(1)>');
  });

  it('SRC-2 no results is a normal page with a message, the text and links onward', () => {
    const { el } = render(ok([]));

    expect(el.querySelector('.rp-empty h2')!.textContent).toContain('Nothing found for “frock”');
    expect([...el.querySelectorAll('.rp-empty a')].map((a) => a.getAttribute('href'))).toEqual(['/new-in']);
    expect(el.querySelector('rp-filter-panel')).toBeNull();
  });

  it('SRC-2 filters that remove everything say so and keep the way back', () => {
    const { el } = render(ok([]), { q: 'frock', size: '5-6y' });

    expect(el.querySelector('.rp-empty h2')!.textContent).toContain('Nothing matches those filters');
    expect(el.querySelector('rp-active-filters')).not.toBeNull();
  });

  it('SRC-9 the filter form and the page links keep the search text', () => {
    const { el } = render(ok(Array.from({ length: 24 }, (_, i) => tile(i + 1)), { total: 60, totalPages: 3 }));

    expect(el.querySelector<HTMLInputElement>('rp-filter-panel input[type="hidden"][name="q"]')!.value).toBe('frock');
    expect(el.querySelector('rp-filter-panel option[value="recent"]')!.textContent).toContain('Best match');
    expect(el.querySelector('rp-pagination a[rel="next"]')!.getAttribute('href')).toBe('/search?q=frock&page=2');
  });

  it('SRC-4 an exact SKU answers 302 with the product address', () => {
    const init: ResponseInit = {};
    render({ status: 'redirect', url: '/girls/dresses/floral?colour=navy&size=5-6y' }, { q: 'romp-flr-navy-5-6y' }, init);

    expect(init.status).toBe(302);
    expect(new Headers(init.headers).get('Location')).toBe('/girls/dresses/floral?colour=navy&size=5-6y');
  });

  it('SRC-2 answers 200 for results and for none, 404 past the last page and 503 on failure', () => {
    for (const [result, status] of [[ok([tile(1)]), 200], [ok([]), 200], [{ status: 'notFound' }, 404], [{ status: 'error' }, 503]] as const) {
      TestBed.resetTestingModule();
      const init: ResponseInit = {};
      render(result, { q: 'frock' }, init);
      expect(init.status).toBe(status);
    }
  });

  it('SRC-11 result pages are noindex, follow, with a canonical to the search itself', () => {
    render(ok([tile(1)]), { q: 'frock', page: '2' });
    // page 2 is not asked for from the API result here; the canonical never carries paging or filters.
    expect(document.head.querySelector('meta[name="robots"]')!.getAttribute('content')).toBe('noindex, follow');
    expect(document.head.querySelector('link[rel="canonical"]')!.getAttribute('href')).toContain('/search?q=frock');
    expect(document.head.querySelector('link[rel="canonical"]')!.getAttribute('href')).not.toContain('page=');
  });

  it('SEO-06 untidy text or unknown keys redirect (301) to the one clean address, without asking the API', () => {
    const init: ResponseInit = {};
    const { find } = render(null, { q: '  Red   FROCK ', utm_source: 'x' }, init);

    expect(init.status).toBe(301);
    expect(new Headers(init.headers).get('Location')).toBe('/search?q=red%20frock');
    expect(find).not.toHaveBeenCalled();
  });

  it('SRC-2 nothing typed shows just the search form, answers 200 and asks nothing', () => {
    const init: ResponseInit = {};
    const { el, find } = render(null, {}, init);

    expect(el.querySelector('h1')!.textContent).toContain('Search');
    expect(el.querySelector('form[role="search"]')).not.toBeNull();
    expect(init.status).toBe(200);
    expect(find).not.toHaveBeenCalled();
  });

  it('SRC-9 applying filters with scripting navigates with the text kept', () => {
    const { el } = render(ok([tile(1)]));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    el.querySelector<HTMLInputElement>('rp-filter-panel input[name="size"]')!.click();
    el.querySelector<HTMLFormElement>('rp-filter-panel form')!.dispatchEvent(new Event('submit', { cancelable: true }));

    expect(navigate).toHaveBeenCalledWith(['/search'], { queryParams: { q: 'frock', size: ['5-6y'] } });
  });
});
