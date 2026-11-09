import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { SearchApi } from '../../search/services/search-api.service';
import { EMPTY_FILTER } from '../models/listing-query';
import { ListingsApi } from './listings-api.service';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { http: TestBed.inject(HttpTestingController) };
}

describe('count calls for the filter sheet', () => {
  it('FLT-13 the listing count asks for one piece on page 1 with the same filter rules, and returns the total', () => {
    const { http } = setup();
    let total: number | null = -1;
    TestBed.inject(ListingsApi).count({ kind: 'audience', gender: 'girls' }, { ...EMPTY_FILTER, sizes: ['5-6y'], page: 4 }).subscribe((n) => (total = n));
    const req = http.expectOne((r) => r.url === '/api/storefront/listings');
    expect(req.request.params.get('pageSize')).toBe('1');
    expect(req.request.params.has('page')).toBe(false);
    expect(req.request.params.get('size')).toBe('5-6y');
    expect(req.request.params.get('gender')).toBe('girls');
    req.flush({ total: 12 });
    expect(total).toBe(12);
  });

  it('FLT-13 a failed listing count is unknown, not an error', () => {
    const { http } = setup();
    let total: number | null = -1;
    TestBed.inject(ListingsApi).count({ kind: 'new-in' }, EMPTY_FILTER).subscribe((n) => (total = n));
    http.expectOne((r) => r.url === '/api/storefront/listings').flush('x', { status: 503, statusText: 'down' });
    expect(total).toBeNull();
  });

  it('FLT-13 the search count keeps the words, asks for one piece, and returns the total', () => {
    const { http } = setup();
    let total: number | null = -1;
    TestBed.inject(SearchApi).count('frock', { ...EMPTY_FILTER, colours: ['red'] }).subscribe((n) => (total = n));
    const req = http.expectOne((r) => r.url === '/api/storefront/search');
    expect(req.request.params.get('q')).toBe('frock');
    expect(req.request.params.get('pageSize')).toBe('1');
    expect(req.request.params.get('colour')).toBe('red');
    req.flush({ total: 3 });
    expect(total).toBe(3);
  });

  it('FLT-13 a failed search count is unknown', () => {
    const { http } = setup();
    let total: number | null = -1;
    TestBed.inject(SearchApi).count('frock', EMPTY_FILTER).subscribe((n) => (total = n));
    http.expectOne((r) => r.url === '/api/storefront/search').error(new ProgressEvent('error'));
    expect(total).toBeNull();
  });
});
