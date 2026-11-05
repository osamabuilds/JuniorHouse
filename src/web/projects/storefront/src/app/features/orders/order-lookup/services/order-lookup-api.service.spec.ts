import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LookupResult } from '../models/order-lookup.model';
import { OrderLookupApi } from './order-lookup-api.service';

function setUp() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(OrderLookupApi), http: TestBed.inject(HttpTestingController) };
}

describe('OrderLookupApi', () => {
  const answer = (status: number) => {
    const { api, http } = setUp();
    let result: LookupResult | undefined;
    api.lookup({ orderNumber: 'RMP-2026-00001-4', mobile: '0300', captchaToken: null }).subscribe((r) => (result = r));
    http.expectOne('/customer-api/orders/lookup').flush({ title: 'x' }, { status, statusText: 'x' });
    return result;
  };

  it('OLK-2 a 404 is the one mismatch', () => expect(answer(404)).toEqual({ kind: 'mismatch' }));
  it('OLK-4 a 428 means the check is needed', () => expect(answer(428)).toEqual({ kind: 'captcha' }));
  it('OLK-3 a 429 is the rate limit', () => expect(answer(429)).toEqual({ kind: 'limited' }));
  it('any other failure is an error, never a throw', () => expect(answer(500)).toEqual({ kind: 'error' }));

  it('OLK-5 the link is sent as a query parameter, not in the path', () => {
    const { api, http } = setUp();
    api.byLink('a b').subscribe();

    const request = http.expectOne((r) => r.url === '/customer-api/orders/lookup/link');

    expect(request.request.params.get('token')).toBe('a b');
    request.flush({}, { status: 404, statusText: 'x' });
  });
});
