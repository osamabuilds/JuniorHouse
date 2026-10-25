import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { EMPTY_FILTER } from '../../listings';
import { SearchPageData, SearchResult } from '../models/search.model';
import { SearchApi } from './search-api.service';

const page = (total: number): SearchPageData => ({
  query: 'frock',
  tiles: [],
  total,
  page: 1,
  pageSize: 24,
  totalPages: 1,
  maxSavingPercent: 0,
  options: { sizes: [], colours: [], priceMin: null, priceMax: null, ages: [] },
});

describe('SearchApi', () => {
  let api: SearchApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(SearchApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const run = (query: string, filter = EMPTY_FILTER): SearchResult[] => {
    const out: SearchResult[] = [];
    api.find(query, filter).subscribe((r) => out.push(r));
    return out;
  };

  it('SRC-1 asks the search endpoint with the text and the canonical filter', () => {
    const out = run('red frock', { ...EMPTY_FILTER, sizes: ['5-6y'], sort: 'price-asc', page: 2 });

    const req = http.expectOne((r) => r.url === '/api/storefront/search');
    expect(req.request.params.get('q')).toBe('red frock');
    expect(req.request.params.getAll('size')).toEqual(['5-6y']);
    expect(req.request.params.get('sort')).toBe('price-asc');
    expect(req.request.params.get('page')).toBe('2');
    req.flush(page(3));

    expect(out).toEqual([{ status: 'ok', page: page(3), suggestions: [] }]);
  });

  it('SRC-4 an exact SKU goes straight to its product, with colour and size selected', () => {
    const out = run('romp-flr-navy-5-6y');

    http.expectOne((r) => r.url === '/api/storefront/search/sku').flush({ path: '/girls/dresses/floral', colour: 'navy', size: '5-6y' });

    expect(out).toEqual([{ status: 'redirect', url: '/girls/dresses/floral?colour=navy&size=5-6y' }]);
    http.expectNone('/api/storefront/search');
  });

  it('SRC-4 text shaped like a code that is not one is searched by words', () => {
    const out = run('dungaree');

    http.expectOne((r) => r.url === '/api/storefront/search/sku').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne((r) => r.url === '/api/storefront/search').flush(page(2));

    expect(out[0].status).toBe('ok');
  });

  it('SRC-3 text with spaces never asks the SKU endpoint', () => {
    run('red frock');

    http.expectNone((r) => r.url === '/api/storefront/search/sku');
    http.expectOne((r) => r.url === '/api/storefront/search').flush(page(1));
  });

  it('SRC-2 an empty answer fetches the category links, and a failing link request still answers', () => {
    const out = run('qzxjvk x');

    http.expectOne((r) => r.url === '/api/storefront/search').flush(page(0));
    http.expectOne((r) => r.url === '/api/storefront/listings/empty').flush({ suggestions: [{ label: 'New in', path: '/new-in' }] });

    expect(out).toEqual([{ status: 'ok', page: page(0), suggestions: [{ label: 'New in', path: '/new-in' }] }]);
  });

  it('SRC-2 a failing empty-state request does not fail the page', () => {
    const out = run('qzxjvk x');

    http.expectOne((r) => r.url === '/api/storefront/search').flush(page(0));
    http.expectOne((r) => r.url === '/api/storefront/listings/empty').flush(null, { status: 500, statusText: 'Server Error' });

    expect(out).toEqual([{ status: 'ok', page: page(0), suggestions: [] }]);
  });

  it('a page after the last is notFound and a server failure is an error, neither throws', () => {
    const missing = run('a b');
    http.expectOne((r) => r.url === '/api/storefront/search').flush(null, { status: 404, statusText: 'Not Found' });
    const broken = run('c d');
    http.expectOne((r) => r.url === '/api/storefront/search').flush(null, { status: 500, statusText: 'Server Error' });

    expect(missing).toEqual([{ status: 'notFound' }]);
    expect(broken).toEqual([{ status: 'error' }]);
  });

  it('SRC-1 suggest sends the text and falls back to nothing on failure', () => {
    const out: unknown[] = [];
    api.suggest('dung').subscribe((r) => out.push(r));

    const req = http.expectOne((r) => r.url === '/api/storefront/search/suggest');
    expect(req.request.params.get('q')).toBe('dung');
    req.flush(null, { status: 500, statusText: 'Server Error' });

    expect(out).toEqual([{ query: 'dung', items: [] }]);
  });
});
