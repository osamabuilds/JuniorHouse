import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StartResult, StatusResult } from '../models/payment.model';
import { PaymentApi } from './payment-api.service';

describe('PaymentApi', () => {
  let api: PaymentApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(PaymentApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  function startWith(status: number, body: object | null): StartResult {
    let result!: StartResult;
    api.start('tok').subscribe((r) => (result = r));
    http.expectOne('/customer-api/orders/payments/start').flush(body, { status, statusText: 'x' });
    return result;
  }

  function statusWith(status: number, body: object | null): StatusResult {
    let result!: StatusResult;
    api.status('tok').subscribe((r) => (result = r));
    http.expectOne((r) => r.url === '/customer-api/orders/payments/status').flush(body, { status, statusText: 'x' });
    return result;
  }

  it('PAY-3 starting sends the token and nothing else: no amount, order id or method', () => {
    api.start('tok').subscribe();
    const req = http.expectOne('/customer-api/orders/payments/start');

    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: 'tok' });
    req.flush({ kind: 'Started', redirectUrl: '/pay' });
  });

  it('PAY-3 an opened page comes back as a redirect address', () => {
    expect(startWith(200, { kind: 'Started', redirectUrl: '/customer-api/orders/payments/fake/fk_1' })).toEqual({ status: 'ok', kind: 'Started', redirectUrl: '/customer-api/orders/payments/fake/fk_1' });
    expect(startWith(200, { kind: 'Reused', redirectUrl: '/p' })).toEqual({ status: 'ok', kind: 'Reused', redirectUrl: '/p' });
  });

  it('PAY-7 a 503 says the gateway is unavailable, in words the page can show', () => {
    expect(startWith(503, { kind: 'GatewayUnavailable', redirectUrl: null })).toEqual({ status: 'ok', kind: 'GatewayUnavailable', redirectUrl: null });
  });

  it('PAY-11 a 409 carries the reason the order can no longer be paid', () => {
    expect(startWith(409, { kind: 'WindowClosed', redirectUrl: null })).toEqual({ status: 'ok', kind: 'WindowClosed', redirectUrl: null });
    expect(startWith(409, { kind: 'UnderReview', redirectUrl: null })).toEqual({ status: 'ok', kind: 'UnderReview', redirectUrl: null });
  });

  it('SEC-05 a 404 is simply an invalid link, with no detail', () => {
    expect(startWith(404, null)).toEqual({ status: 'invalid' });
    expect(statusWith(404, null)).toEqual({ status: 'invalid' });
  });

  it('a server error or a lost connection is a plain error, never a thrown exception', () => {
    expect(startWith(500, null)).toEqual({ status: 'error' });
    expect(statusWith(500, null)).toEqual({ status: 'error' });
    let result!: StatusResult;
    api.status('tok').subscribe((r) => (result = r));
    http.expectOne((r) => r.url === '/customer-api/orders/payments/status').error(new ProgressEvent('error'));
    expect(result).toEqual({ status: 'error' });
  });

  it('PAY-10 the status carries the token in the query string and returns the stored state', () => {
    let result!: StatusResult;
    api.status('a b+c').subscribe((r) => (result = r));
    const req = http.expectOne((r) => r.url === '/customer-api/orders/payments/status');

    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('token')).toBe('a b+c');
    req.flush({ state: 'paid', orderNumber: 'RMP-2026-00001-4', total: 3150, canRetry: false, methodCode: 'CARD' });
    expect(result).toEqual({ status: 'ok', payment: { state: 'paid', orderNumber: 'RMP-2026-00001-4', total: 3150, canRetry: false, methodCode: 'CARD' } });
  });
});
