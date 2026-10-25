import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { provideSiteSettings } from '../../../site-settings';
import { ListingResult, ListingTile } from '../../models/listing.model';
import { ListingsApi } from '../../services/listings-api.service';
import { provideListings } from '../../listings.providers';
import { ListingPageContainer } from './listing-page.container';

const tile = (n: number, extra: Partial<ListingTile> = {}): ListingTile => ({
  path: `/girls/dresses/dress-${n}`,
  name: `Dress ${n}`,
  price: 800,
  compareAtPrice: null,
  savingPercent: 0,
  savingAmount: 0,
  colourCount: 1,
  hasSoldOutSizes: false,
  sizes: [],
  image: { altText: `Dress ${n}`, width: 800, height: 1000, sources: [{ width: 320, webp: '/media/h-320.webp', jpeg: '/media/h-320.jpg' }, { width: 640, webp: '/media/h-640.webp', jpeg: '/media/h-640.jpg' }] },
  ...extra,
});

const ok = (tiles: ListingTile[], over: Record<string, unknown> = {}): ListingResult => ({
  status: 'ok',
  page: {
    title: 'Dresses', scopeKind: 'Category', audienceName: 'Girls', categoryName: 'Dresses',
    breadcrumbs: [{ label: 'Home', path: '/' }, { label: 'Girls', path: '/girls' }, { label: 'Dresses', path: null }],
    tiles, total: tiles.length, page: 1, pageSize: 24, totalPages: 1, maxSavingPercent: 0, ...over,
  },
  options: { sizes: [{ value: '5-6y', label: '5-6 Years' }], colours: [{ value: 'red', label: 'Red' }], priceMin: 500, priceMax: 900, ages: [] },
  empty: tiles.length === 0 ? { suggestions: [{ label: 'New in', path: '/girls/new-in' }] } : null,
});

function render(result: ListingResult, query: Record<string, string | string[]> = {}, responseInit?: ResponseInit): { el: HTMLElement; get: ReturnType<typeof vi.fn> } {
  const get = vi.fn(() => of(result));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      provideListings(),
      { provide: ListingsApi, useValue: { get } },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
      {
        provide: ActivatedRoute,
        useValue: {
          paramMap: of(convertToParamMap({ gender: 'girls', category: 'dresses' })),
          queryParamMap: of(convertToParamMap(query)),
          data: of({ kind: 'category' }),
          snapshot: {},
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(ListingPageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  return { el: fixture.nativeElement as HTMLElement, get };
}

describe('ListingPageContainer', () => {
  it('LST-12 shows one h1, the breadcrumb, and the tiles as articles in a list', () => {
    const { el } = render(ok([tile(1), tile(2)]));
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Dresses');
    expect(el.querySelector('nav[aria-label="Breadcrumb"] [aria-current="page"]')!.textContent).toContain('Dresses');
    expect(el.querySelectorAll('ul.rp-grid > li > rp-product-tile article').length).toBe(2);
  });

  it('LST-12 the first row loads eagerly, the first picture at high priority, the rest lazily', () => {
    const { el } = render(ok(Array.from({ length: 6 }, (_, i) => tile(i + 1))));
    const imgs = [...el.querySelectorAll('img')];
    expect(imgs.slice(0, 4).every((i) => i.getAttribute('loading') === 'eager')).toBe(true);
    expect(imgs[0].getAttribute('fetchpriority')).toBe('high');
    expect(imgs[4].getAttribute('loading')).toBe('lazy');
    expect(imgs[0].getAttribute('width')).toBe('800');
    expect(imgs[0].getAttribute('alt')).toBe('Dress 1');
  });

  it('LST-12 each picture offers WebP and JPEG at every generated width', () => {
    const { el } = render(ok([tile(1)]));
    const sources = [...el.querySelectorAll('picture source')];
    expect(sources.map((s) => s.getAttribute('type'))).toEqual(['image/webp', 'image/jpeg']);
    expect(sources[0].getAttribute('srcset')).toBe('/media/h-320.webp 320w, /media/h-640.webp 640w');
  });

  it('LST-12 the filters are a real GET form with labelled fieldsets, working without scripting', () => {
    const { el } = render(ok([tile(1)]));
    const form = el.querySelector('form.rp-filters')!;
    expect(form.getAttribute('method')).toBe('get');
    expect(form.getAttribute('action')).toBe('/girls/dresses');
    expect([...form.querySelectorAll('legend')].map((l) => l.textContent!.trim())).toEqual(['Size', 'Colour', 'Price (Rs.)', 'Sort']);
    expect(form.querySelector('input[type="checkbox"][name="size"]')).not.toBeNull();
    expect(form.querySelector('button[type="submit"]')).not.toBeNull();
  });

  it('LST-3 active filters are chips that link to the listing without them, with a clear link', () => {
    const { el } = render(ok([tile(1)]), { size: '5-6y' });
    const chip = el.querySelector<HTMLAnchorElement>('.rp-active a.rp-chip')!;
    expect(chip.textContent).toContain('Size: 5-6 Years');
    expect(chip.getAttribute('href')).toBe('/girls/dresses');
    expect(el.querySelector('.rp-active a.rp-link')!.textContent).toContain('Clear filters');
  });

  it('LST-6 a filter combination with no results explains itself and offers a way out', () => {
    const { el } = render(ok([]), { size: '5-6y' });
    expect(el.querySelector('.rp-empty h2')!.textContent).toContain('Nothing matches');
    expect(el.querySelector('.rp-empty a[href="/girls/new-in"]')).not.toBeNull();
    expect(el.querySelectorAll('rp-product-tile').length).toBe(0);
  });

  it('LST-4 pagination keeps the filter in its links and marks the current page', () => {
    const { el } = render(ok([tile(1)], { page: 2, totalPages: 3, total: 50 }), { sort: 'price-asc', page: '2' });
    const nav = el.querySelector('nav[aria-label="Pagination"]')!;
    expect(nav.querySelector('[aria-current="page"]')!.textContent).toContain('2');
    expect(nav.querySelector('a[rel="next"]')!.getAttribute('href')).toBe('/girls/dresses?sort=price-asc&page=3');
    expect(nav.querySelector('a[rel="prev"]')!.getAttribute('href')).toBe('/girls/dresses?sort=price-asc');
  });

  it('LST-11 an unknown scope renders the not-found page with a real 404', () => {
    const init: ResponseInit = {};
    const { el } = render({ status: 'notFound' }, {}, init);
    expect(init.status).toBe(404);
    expect(el.querySelector('h1')!.textContent).toContain('could not find');
  });

  it('LST-10 a query with unknown keys is redirected to the clean one (301 on the server)', () => {
    const init: ResponseInit = {};
    const { get } = render(ok([tile(1)]), { utm_source: 'x', size: '5-6y' }, init);
    expect(init.status).toBe(301);
    expect(new Headers(init.headers).get('Location')).toBe('/girls/dresses?size=5-6y');
    expect(get).not.toHaveBeenCalled();
  });

  it('LST-11 the no-script price boxes are folded into one price range by a redirect', () => {
    const init: ResponseInit = {};
    render(ok([tile(1)]), { priceMin: '500', priceMax: '900' }, init);
    expect(init.status).toBe(301);
    expect(new Headers(init.headers).get('Location')).toBe('/girls/dresses?price=500-900');
  });
});
