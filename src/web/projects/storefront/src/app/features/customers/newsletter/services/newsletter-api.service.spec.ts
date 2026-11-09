import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NewsletterApi } from './newsletter-api.service';

describe('NewsletterApi', () => {
  let http: HttpTestingController;
  let api: NewsletterApi;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(NewsletterApi);
  });

  afterEach(() => http.verify());

  it('NLS-1 subscribe posts only the email to the same-origin pass-through', () => {
    let result = '';
    api.subscribe('sara@example.com').subscribe((r) => (result = r));
    const req = http.expectOne('/customer-api/newsletter/subscribe');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'sara@example.com' });
    req.flush({ subscribed: true });
    expect(result).toBe('ok');
  });

  it.each([
    [400, 'invalid'],
    [422, 'invalid'],
    [429, 'busy'],
    [500, 'error'],
    [0, 'error'],
  ])('NLS-6 subscribe maps HTTP %i to %s without throwing', (status, expected) => {
    let result = '';
    api.subscribe('sara@example.com').subscribe((r) => (result = r));
    http.expectOne('/customer-api/newsletter/subscribe').flush(null, { status, statusText: 'x' });
    expect(result).toBe(expected);
  });

  it('NLS-2 reads and sets the caller own preference', () => {
    let read: unknown;
    let set: unknown;
    api.mine().subscribe((r) => (read = r));
    http.expectOne({ method: 'GET', url: '/customer-api/newsletter' }).flush({ subscribed: true });
    api.setMine(false).subscribe((r) => (set = r));
    const put = http.expectOne({ method: 'PUT', url: '/customer-api/newsletter' });
    expect(put.request.body).toEqual({ subscribed: false });
    put.flush({ subscribed: false });
    expect(read).toEqual({ status: 'ok', subscribed: true });
    expect(set).toEqual({ status: 'ok', subscribed: false });
  });

  it('NLS-2 a signed-out caller is told so, any other failure is an error', () => {
    const results: unknown[] = [];
    api.mine().subscribe((r) => results.push(r));
    http.expectOne('/customer-api/newsletter').flush(null, { status: 401, statusText: 'Unauthorized' });
    api.setMine(true).subscribe((r) => results.push(r));
    http.expectOne('/customer-api/newsletter').flush(null, { status: 500, statusText: 'Server Error' });
    expect(results).toEqual([{ status: 'signedOut' }, { status: 'error' }]);
  });
});
