import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ParcelResult } from '../models/shipment-tracking.model';
import { ParcelTrackingApi } from './parcel-tracking-api.service';

function setUp() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(ParcelTrackingApi), http: TestBed.inject(HttpTestingController) };
}

describe('ParcelTrackingApi (TRK-9, TRK-10)', () => {
  const answer = (status: number) => {
    const { api, http } = setUp();
    let result: ParcelResult | undefined;
    api.track('WB-1', null).subscribe((r) => (result = r));
    http.expectOne((r) => r.url === '/customer-api/shipping/track').flush({ title: 'x' }, { status, statusText: 'x' });
    return result;
  };

  it('TRK-10 a 404 is the one generic miss', () => expect(answer(404)).toEqual({ kind: 'notfound' }));
  it('TRK-10 a 428 means the check is needed', () => expect(answer(428)).toEqual({ kind: 'captcha' }));
  it('TRK-10 a 429 is the rate limit', () => expect(answer(429)).toEqual({ kind: 'limited' }));
  it('any other failure is an error, never a throw', () => expect(answer(500)).toEqual({ kind: 'error' }));

  it('TRK-9 the number goes in the query and the check token only when there is one', () => {
    const { api, http } = setUp();
    api.track('WB 1', 'tok').subscribe();

    const request = http.expectOne((r) => r.url === '/customer-api/shipping/track');

    expect(request.request.params.get('waybill')).toBe('WB 1');
    expect(request.request.params.get('captchaToken')).toBe('tok');
    request.flush({}, { status: 404, statusText: 'x' });
  });
});
