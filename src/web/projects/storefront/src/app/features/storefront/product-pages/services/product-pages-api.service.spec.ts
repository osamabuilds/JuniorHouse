import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProductPage, ProductPageResponse, ProductPageResult } from '../models/product-page.model';
import { ProductPagesApi } from './product-pages-api.service';

const page = { path: '/girls/dresses/floral', name: 'Floral' } as ProductPage;

function run(body: ProductPageResponse | null, selection = { colour: null as string | null, size: null as string | null }) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  const api = TestBed.inject(ProductPagesApi);
  const http = TestBed.inject(HttpTestingController);
  let out: ProductPageResult | undefined;
  api.get('/girls/dresses/floral', selection).subscribe((r) => (out = r));
  const req = http.expectOne((r) => r.url === '/api/storefront/products/by-path');
  if (body) req.flush(body);
  else req.flush('', { status: 500, statusText: 'x' });
  http.verify();
  return { out: out!, params: req.request.params };
}

describe('ProductPagesApi', () => {
  it('PDP-1 returns the page when Found', () => {
    expect(run({ outcome: 'Found', path: null, page }).out).toEqual({ status: 'found', page });
  });

  it('PDP-13 maps the other outcomes, taking the redirect target from the answer', () => {
    expect(run({ outcome: 'Moved', path: '/girls/dresses/new', page: null }).out).toEqual({ status: 'moved', path: '/girls/dresses/new' });
    expect(run({ outcome: 'Gone', path: null, page: null }).out).toEqual({ status: 'gone' });
    expect(run({ outcome: 'NotFound', path: null, page: null }).out).toEqual({ status: 'notFound' });
  });

  it('a Moved answer without a target is not a redirect, and a Found answer without a page is an error', () => {
    expect(run({ outcome: 'Moved', path: null, page: null }).out).toEqual({ status: 'notFound' });
    expect(run({ outcome: 'Found', path: null, page: null }).out).toEqual({ status: 'error' });
  });

  it('a failed call is an error, never a 404', () => {
    expect(run(null).out).toEqual({ status: 'error' });
  });

  it('PDP-2 sends the path and only the choices that were made', () => {
    const { params } = run({ outcome: 'NotFound', path: null, page: null }, { colour: 'navy', size: null });
    expect([params.get('path'), params.get('colour'), params.has('size')]).toEqual(['/girls/dresses/floral', 'navy', false]);
  });
});
