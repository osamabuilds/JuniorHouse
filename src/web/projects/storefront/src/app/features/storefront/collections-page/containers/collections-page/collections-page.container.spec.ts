import { DOCUMENT } from '@angular/common';
import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { provideSiteSettings } from '../../../site-settings';
import { CollectionsPageResult, CollectionTile } from '../../models/collections-page.model';
import { CollectionsPageApi } from '../../services/collections-page-api.service';
import { provideCollectionsPage } from '../../collections-page.providers';
import { CollectionsPageContainer } from './collections-page.container';

const item = (n: number): CollectionTile => ({ code: 'C' + n, name: 'Collection ' + n, path: '/collections/c' + n, pieces: n, image: null });
const ok = (n: number, over: Record<string, unknown> = {}): CollectionsPageResult => ({
  status: 'ok',
  page: { items: Array.from({ length: n }, (_, i) => item(i + 1)), total: n, page: 1, pageSize: 24, totalPages: 1, ...over },
});

function render(result: CollectionsPageResult, query: Record<string, string> = {}, responseInit?: ResponseInit) {
  const get = vi.fn(() => of(result));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      provideCollectionsPage(),
      { provide: CollectionsPageApi, useValue: { get } },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
      { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap(query)), paramMap: of(convertToParamMap({})), data: of({}), snapshot: {} } },
    ],
  });
  const fixture = TestBed.createComponent(CollectionsPageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  return { el: fixture.nativeElement as HTMLElement, get, doc: TestBed.inject(DOCUMENT) };
}

describe('CollectionsPageContainer', () => {
  it('COL-1 COL-4 shows one h1, the breadcrumb and the tiles in a list, with no skipped heading level', () => {
    const { el } = render(ok(3));
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Collections');
    expect(el.querySelector('nav[aria-label="Breadcrumb"] [aria-current="page"]')!.textContent).toContain('Collections');
    expect(el.querySelectorAll('ul.rp-split > li rp-collection-tile').length).toBe(3);
    const levels = [...el.querySelectorAll('h1,h2,h3')].map((h) => Number(h.tagName[1]));
    levels.forEach((l, i) => i > 0 && expect(l - levels[i - 1]).toBeLessThanOrEqual(1));
  });

  it('COL-5 sets a unique title and description, a self-referencing canonical, and is indexable', () => {
    const { doc } = render(ok(2));
    expect(doc.title).toContain('Collections');
    expect(doc.head.querySelector('link[rel="canonical"]')!.getAttribute('href')).toMatch(/\/collections$/);
    expect(doc.head.querySelector('meta[name="description"]')!.getAttribute('content')).toBeTruthy();
    expect(doc.head.querySelector('meta[name="robots"]')).toBeNull();
    const ld = [...doc.head.querySelectorAll('script[type="application/ld+json"]')].map((s) => JSON.parse(s.textContent!));
    expect(ld.some((o) => o['@type'] === 'BreadcrumbList')).toBe(true);
  });

  it('COL-5 answers 200', () => {
    const responseInit: ResponseInit = {};
    render(ok(2), {}, responseInit);
    expect(responseInit.status).toBe(200);
  });

  it('COL-6 when no collection qualifies it is a normal 200 page with a message and a way onward, not a 404', () => {
    const responseInit: ResponseInit = {};
    const { el } = render(ok(0, { total: 0 }), {}, responseInit);
    expect(responseInit.status).toBe(200);
    expect(el.querySelector('h1')!.textContent).toContain('Collections');
    expect(el.textContent).toContain('No collections');
    expect(el.querySelector<HTMLAnchorElement>('a[href="/new-in"]')).not.toBeNull();
    expect(el.querySelector('ul.rp-split')).toBeNull();
  });

  it('COL-1 a page after the last is a real 404', () => {
    const responseInit: ResponseInit = {};
    const { el } = render(ok(0, { total: 5, page: 9, totalPages: 1 }), { page: '9' }, responseInit);
    expect(responseInit.status).toBe(404);
    expect(el.querySelector('ul.rp-split')).toBeNull();
  });

  it('COL-1 a failure from the server is a 503 error page that offers a way onward', () => {
    const responseInit: ResponseInit = {};
    const { el } = render({ status: 'error' }, {}, responseInit);
    expect(responseInit.status).toBe(503);
    expect(el.textContent).toContain('Something went wrong');
  });

  it('COL-1 asks the server for the page in the address, and a text or negative page means the first', () => {
    expect(render(ok(1), { page: '3' }).get).toHaveBeenCalledWith(3);
    TestBed.resetTestingModule();
    expect(render(ok(1), { page: 'abc' }).get).toHaveBeenCalledWith(1);
    TestBed.resetTestingModule();
    expect(render(ok(1), { page: '-4' }).get).toHaveBeenCalledWith(1);
  });

  it('COL-1 more than one page shows the pagination links', () => {
    const { el } = render(ok(2, { totalPages: 3, total: 60 }));
    expect(el.querySelector('nav[aria-label="Pagination"]')).not.toBeNull();
  });
});
