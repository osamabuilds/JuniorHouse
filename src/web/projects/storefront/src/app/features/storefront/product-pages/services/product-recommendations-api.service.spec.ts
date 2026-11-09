import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { ListingTile } from '../../listings/models/listing.model';
import { ProductRecommendations } from '../models/product-recommendations.model';
import { ProductRecommendationsApi } from './product-recommendations-api.service';

const tile = { productId: 2, path: '/girls/dresses/b', name: 'B' } as ListingTile;

function run(respond: (req: ReturnType<HttpTestingController['expectOne']>) => void): ProductRecommendations {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  const http = TestBed.inject(HttpTestingController);
  let out: ProductRecommendations | undefined;
  TestBed.inject(ProductRecommendationsApi).get('/girls/dresses/a').subscribe((r) => (out = r));
  // expectOne fails if there is not exactly one request: the page asks once per product.
  const req = http.expectOne((r) => r.url === '/api/storefront/products/recommendations');
  expect(req.request.params.get('path')).toBe('/girls/dresses/a');
  respond(req);
  http.verify();
  return out!;
}

describe('ProductRecommendationsApi', () => {
  it('REC-1 returns the two rows as the server sent them, in one request', () => {
    expect(run((req) => req.flush({ similar: [tile], trending: [] }))).toEqual({ similar: [tile], trending: [] });
  });

  it('REC-10 a server error or a 404 is empty rows, never an exception', () => {
    expect(run((req) => req.flush('', { status: 500, statusText: 'x' }))).toEqual({ similar: [], trending: [] });
    expect(run((req) => req.flush('', { status: 404, statusText: 'x' }))).toEqual({ similar: [], trending: [] });
  });

  it('REC-10 a network error is empty rows', () => {
    expect(run((req) => req.error(new ProgressEvent('error')))).toEqual({ similar: [], trending: [] });
  });

  it('REC-10 an odd answer is empty rows', () => {
    expect(run((req) => req.flush({ similar: 'nope' }))).toEqual({ similar: [], trending: [] });
    expect(run((req) => req.flush(null))).toEqual({ similar: [], trending: [] });
  });
});
