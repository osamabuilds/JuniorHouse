import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, UrlSegment } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { provideSiteSettings } from '../../../site-settings';
import { ProductPage, ProductPageResult } from '../../models/product-page.model';
import { provideProductPages } from '../../product-pages.providers';
import { ProductPagesApi } from '../../services/product-pages-api.service';
import { ProductPageContainer } from './product-page.container';

const basePage: ProductPage = {
  path: '/girls/dresses/floral-summer-dress',
  name: 'Floral Summer Dress',
  description: 'A light cotton dress.',
  audienceName: 'Girls',
  categoryName: 'Dresses',
  ageName: 'Kids 4-8Y',
  breadcrumbs: [
    { label: 'Home', path: '/' },
    { label: 'Girls', path: '/girls' },
    { label: 'Dresses', path: '/girls/dresses' },
    { label: 'Floral Summer Dress', path: null },
  ],
  price: { price: 1299, compareAtPrice: 1599, savingPercent: 19, isFrom: false },
  selectedColour: 'red',
  selectedSize: '5-6y',
  sku: 'SKU-1',
  colours: [{ code: 'red', name: 'Red', inStock: true }, { code: 'navy', name: 'Navy', inStock: false }],
  sizes: [
    { code: '5-6y', name: '5-6 Years', availability: 'InStock', selectable: true },
    { code: '7-8y', name: '7-8 Years', availability: 'SoldOut', selectable: false },
    { code: '9-10y', name: '9-10 Years', availability: 'NotYetAvailable', selectable: false },
  ],
  images: [
    { altText: 'Front', width: 1200, height: 1600, sources: [{ width: 320, webp: '/media/a-320.webp', jpeg: '/media/a-320.jpg' }, { width: 640, webp: '/media/a-640.webp', jpeg: '/media/a-640.jpg' }] },
    { altText: 'Back', width: 1200, height: 1600, sources: [{ width: 320, webp: '/media/b-320.webp', jpeg: null }] },
  ],
  sizeChart: {
    name: 'Girls dresses',
    rows: [
      { sizeCode: '5-6y', sizeName: '5-6 Years', ageMinMonths: 60, ageMaxMonths: 83, heightMinCm: 110, heightMaxCm: 122, weightMinKg: null, weightMaxKg: null, chestCm: 60, waistCm: null, lengthCm: null, selected: true },
      { sizeCode: '7-8y', sizeName: '7-8 Years', ageMinMonths: 84, ageMaxMonths: 107, heightMinCm: 122, heightMaxCm: 134, weightMinKg: null, weightMaxKg: null, chestCm: 64, waistCm: null, lengthCm: null, selected: false },
    ],
  },
  composition: [{ name: 'Cotton', percent: 95 }, { name: 'Elastane', percent: 5 }],
  care: [{ name: 'Wash at 30', symbolCode: 'wash-30' }],
  careNote: 'Wash separately',
  isAvailable: true,
};

function render(result: ProductPageResult, query: Record<string, string> = {}, responseInit?: ResponseInit) {
  const get = vi.fn(() => of(result));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      provideProductPages(),
      { provide: ProductPagesApi, useValue: { get } },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
      {
        provide: ActivatedRoute,
        useValue: {
          url: of(['girls', 'dresses', 'floral-summer-dress'].map((p) => new UrlSegment(p, {}))),
          queryParamMap: of(convertToParamMap(query)),
          paramMap: of(convertToParamMap({})),
          data: of({}),
          snapshot: {},
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(ProductPageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  return { el: fixture.nativeElement as HTMLElement, get };
}

const found = (over: Partial<ProductPage> = {}): ProductPageResult => ({ status: 'found', page: { ...basePage, ...over } });

describe('ProductPageContainer', () => {
  it('PDP-1 shows one h1 with the name, the breadcrumb, the price, the SKU and the description', () => {
    const { el } = render(found());

    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Floral Summer Dress');
    expect(el.querySelector('nav[aria-label="Breadcrumb"] [aria-current="page"]')!.textContent).toContain('Floral Summer Dress');
    expect(el.querySelector('.rp-price')!.textContent).toContain('1,299');
    expect(el.querySelector('.pdp-sku')!.textContent).toContain('SKU-1');
    expect(el.textContent).toContain('A light cotton dress.');
  });

  it('PDP-1 asks the API for the path and the choice made in the address', () => {
    const { get } = render(found(), { colour: 'Navy', size: '5-6Y', utm_source: 'x' });

    expect(get).toHaveBeenCalledWith('/girls/dresses/floral-summer-dress', { colour: 'navy', size: '5-6y' });
  });

  it('PDP-10 the first picture is eager and high priority, the rest lazy, all with alt text and explicit size', () => {
    const { el } = render(found());
    const imgs = [...el.querySelectorAll('img')];

    expect(imgs.map((i) => i.getAttribute('loading'))).toEqual(['eager', 'lazy']);
    expect(imgs[0].getAttribute('fetchpriority')).toBe('high');
    expect(imgs[1].getAttribute('fetchpriority')).toBeNull();
    expect(imgs.map((i) => i.getAttribute('alt'))).toEqual(['Front', 'Back']);
    expect(imgs[0].getAttribute('width')).toBe('1200');
    expect(imgs[0].getAttribute('height')).toBe('1600');
    const first = [...el.querySelectorAll('picture')[0].querySelectorAll('source')];
    expect(first.map((s) => s.getAttribute('type'))).toEqual(['image/webp', 'image/jpeg']);
    expect(first[0].getAttribute('srcset')).toBe('/media/a-320.webp 320w, /media/a-640.webp 640w');
  });

  it('PDP-9 shows the compare-at price struck through with a spoken "Was", and the saving', () => {
    const { el } = render(found());
    const price = el.querySelector('.rp-price')!;

    expect(price.querySelector('s')!.textContent).toContain('1,599');
    expect(price.querySelector('s .sr-only')!.textContent).toContain('Was');
    expect(price.textContent).toContain('Save 19%');
  });

  it('PDP-15 shows "From" before a size is chosen', () => {
    const { el } = render(found({ price: { price: 1000, compareAtPrice: null, savingPercent: 0, isFrom: true }, selectedSize: null, sku: null }));

    expect(el.querySelector('.rp-price')!.textContent).toContain('From');
    expect(el.querySelector('.pdp-sku')).toBeNull();
  });

  it('PDP-2 colours are links to the product with ?colour=, the chosen one marked, a colour out of stock said in words', () => {
    const { el } = render(found());
    const links = [...el.querySelectorAll('rp-colour-options a')];

    expect(links.map((a) => a.getAttribute('href'))).toEqual(['/girls/dresses/floral-summer-dress?colour=red', '/girls/dresses/floral-summer-dress?colour=navy']);
    expect(links.map((a) => a.getAttribute('aria-current'))).toEqual(['true', null]);
    expect(links[1].textContent).toContain('sold out');
  });

  it('PDP-4 a sold-out size and a size not yet available are text, not links, and say why', () => {
    const { el } = render(found());
    const sizes = [...el.querySelectorAll('rp-size-options li')];

    expect(sizes[0].querySelector('a')!.getAttribute('href')).toBe('/girls/dresses/floral-summer-dress?colour=red&size=5-6y');
    expect(sizes[0].querySelector('a')!.getAttribute('aria-current')).toBe('true');
    expect(sizes[1].querySelector('a')).toBeNull();
    expect(sizes[1].textContent).toContain('Sold out');
    expect(sizes[2].querySelector('a')).toBeNull();
    expect(sizes[2].textContent).toContain('Not yet available');
  });

  it('PDP-6 the size chart is a table with a caption and header scopes, and marks the chosen size in words', () => {
    const { el } = render(found());
    const table = el.querySelector('rp-size-chart table')!;

    expect(table.querySelector('caption')!.textContent).toContain('Girls dresses');
    expect([...table.querySelectorAll('thead th')].every((th) => th.getAttribute('scope') === 'col')).toBe(true);
    expect([...table.querySelectorAll('tbody th')].every((th) => th.getAttribute('scope') === 'row')).toBe(true);
    expect([...table.querySelectorAll('thead th')].map((th) => th.textContent!.trim())).toEqual(['Size', 'Age', 'Height (cm)', 'Chest (cm)']);
    expect(table.querySelector('tr[aria-current="true"]')!.textContent).toContain('your size');
    expect(table.querySelectorAll('tbody tr')[0].textContent).toContain('5 to 6 years');
  });

  it('PDP-7 without a chart the page says so and still shows the rest', () => {
    const { el } = render(found({ sizeChart: null }));

    expect(el.querySelector('rp-size-chart table')).toBeNull();
    expect(el.querySelector('rp-size-chart')!.textContent).toContain('no size guide');
    expect(el.querySelector('rp-size-options')).not.toBeNull();
  });

  it('PDP-8 shows fibre percentages and care instructions in words', () => {
    const { el } = render(found());
    const part = el.querySelector('rp-composition-care')!;

    expect([...part.querySelectorAll('dl dt')].map((e) => e.textContent)).toEqual(['Cotton', 'Elastane']);
    expect([...part.querySelectorAll('dl dd')].map((e) => e.textContent)).toEqual(['95%', '5%']);
    expect(part.querySelector('ul')!.textContent).toContain('Wash at 30');
    expect(part.textContent).toContain('Wash separately');
  });

  it('PDP-11 headings never skip a level: one h1, then only h2', () => {
    const { el } = render(found());

    expect([...el.querySelectorAll('h1,h2,h3,h4')].map((h) => h.tagName)).toEqual(['H1', 'H2', 'H2', 'H2']);
  });

  it('PDP-16 a product with nothing in stock shows a sold-out state and keeps the page', () => {
    const { el } = render(found({ isAvailable: false, selectedSize: null, sku: null, sizes: basePage.sizes.map((s) => ({ ...s, selectable: false, availability: 'SoldOut' })) }));

    expect(el.querySelector('rp-sold-out-state')!.textContent).toContain('Sold out');
    expect(el.querySelector('h1')!.textContent).toContain('Floral Summer Dress');
    expect(el.querySelectorAll('rp-size-options a').length).toBe(0);
    expect(el.querySelector('button')).toBeNull();
  });

  it('PDP-17 words availability as a guide and shows no purchase control or count', () => {
    const { el } = render(found());

    expect(el.querySelector('rp-availability-note')!.textContent).toContain('Size 5-6 Years: in stock.');
    expect(el.querySelector('rp-availability-note')!.textContent).toContain('guide');
    expect(el.textContent).not.toMatch(/reserved|\d+ left|add to (cart|bag)/i);
  });

  it('PDP-13 answers 200 for a page, 404 for a missing one and 410 for a removed one', () => {
    const ok: ResponseInit = {};
    render(found(), {}, ok);
    expect(ok.status).toBe(200);
    TestBed.resetTestingModule();

    const missing: ResponseInit = {};
    const { el } = render({ status: 'notFound' }, {}, missing);
    expect(missing.status).toBe(404);
    expect(el.querySelector('h1')!.textContent).toContain('could not find');
    TestBed.resetTestingModule();

    const gone: ResponseInit = {};
    render({ status: 'gone' }, {}, gone);
    expect(gone.status).toBe(410);
  });

  it('PDP-13 an old address answers 301 with the new path from the API, keeping the visitor choice', () => {
    const init: ResponseInit = {};
    render({ status: 'moved', path: '/girls/dresses/new-name' }, { colour: 'navy' }, init);

    expect(init.status).toBe(301);
    expect(new Headers(init.headers).get('Location')).toBe('/girls/dresses/new-name?colour=navy');
  });

  it('a failed call answers 503, never a cached 404', () => {
    const init: ResponseInit = {};
    render({ status: 'error' }, {}, init);

    expect(init.status).toBe(503);
  });
});
