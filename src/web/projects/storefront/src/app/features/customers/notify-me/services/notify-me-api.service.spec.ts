import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotifyResult } from '../models/notify-me.model';
import { NotifyMeApi } from './notify-me-api.service';

describe('NotifyMeApi', () => {
  let http: HttpTestingController;
  let api: NotifyMeApi;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(NotifyMeApi);
  });

  afterEach(() => http.verify());

  it('NTM-2 posts the size and the email to the same-origin pass-through', () => {
    let result: NotifyResult | undefined;
    api.request(42, 'sara@example.com').subscribe((r) => (result = r));
    const req = http.expectOne('/customer-api/notify-me');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ variantId: 42, email: 'sara@example.com' });
    req.flush({ requested: true });
    expect(result).toEqual({ status: 'ok' });
  });

  it('NTM-6 a 400 carries the first message the server gave', () => {
    let result: NotifyResult | undefined;
    api.request(42, 'x').subscribe((r) => (result = r));
    http.expectOne('/customer-api/notify-me').flush({ errors: { Email: ['That does not look like an email address.'] } }, { status: 400, statusText: 'Bad Request' });
    expect(result).toEqual({ status: 'invalid', message: 'That does not look like an email address.' });
  });

  it('NTM-6 a 400 without a message still says what to do', () => {
    let result: NotifyResult | undefined;
    api.request(42, 'x').subscribe((r) => (result = r));
    http.expectOne('/customer-api/notify-me').flush(null, { status: 400, statusText: 'Bad Request' });
    expect(result?.status).toBe('invalid');
  });

  it.each([
    [429, 'busy'],
    [500, 'error'],
    [0, 'error'],
  ])('NTM-7 maps HTTP %i to %s without throwing', (status, expected) => {
    let result: NotifyResult | undefined;
    api.request(42, 'sara@example.com').subscribe((r) => (result = r));
    http.expectOne('/customer-api/notify-me').flush(null, { status, statusText: 'x' });
    expect(result?.status).toBe(expected);
  });
});
