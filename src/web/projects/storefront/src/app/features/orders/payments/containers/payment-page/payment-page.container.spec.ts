import { ApplicationRef, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideStore } from '@ngrx/store';
import { Observable, of } from 'rxjs';
import { afterEach, vi } from 'vitest';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { PaymentState, PaymentStatus, StartResult, StatusResult } from '../../models/payment.model';
import { providePayments } from '../../payment.providers';
import { PaymentApi } from '../../services/payment-api.service';
import { PaymentPageContainer, isSafePaymentUrl } from './payment-page.container';

const payment = (state: PaymentState, canRetry = false): PaymentStatus => ({ state, orderNumber: 'RMP-2026-00001-4', total: 3150, canRetry, methodCode: 'CARD' });

interface Options {
  mode?: 'start' | 'return';
  token?: string | null;
  start?: StartResult;
  status?: StatusResult;
  responseInit?: ResponseInit;
}

function render(o: Options = {}) {
  vi.useFakeTimers();
  const start = vi.fn((_t: string): Observable<StartResult> => of(o.start ?? { status: 'ok', kind: 'Started', redirectUrl: null }));
  const status = vi.fn((_t: string): Observable<StatusResult> => of(o.status ?? { status: 'ok', payment: payment('confirming') }));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideEffects(),
      provideSiteSettings(),
      providePayments(),
      { provide: PaymentApi, useValue: { start, status } },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { data: { mode: o.mode ?? 'return' }, queryParamMap: convertToParamMap(o.token === null ? {} : { token: o.token ?? 'tok' }) } },
      },
      ...(o.responseInit ? [{ provide: RESPONSE_INIT, useValue: o.responseInit }] : []),
    ],
  });
  const fixture = TestBed.createComponent(PaymentPageContainer);
  fixture.detectChanges();
  // The first request is made after the first render, and the first poll fires on the next timer tick.
  const app = TestBed.inject(ApplicationRef);
  app.tick();
  vi.advanceTimersByTime(5);
  app.tick();
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, start, status };
}

describe('PaymentPageContainer', () => {
  afterEach(() => vi.useRealTimers());

  it('PAY-10 the page is noindex and its response is never cached', () => {
    const init: ResponseInit = {};

    render({ responseInit: init });

    expect(TestBed.inject(Meta).getTag('name="robots"')!.content).toBe('noindex, nofollow');
    expect(new Headers(init.headers).get('Cache-Control')).toBe('private, no-store');
  });

  it('SEC-05 with no token it asks for nothing and points to the track-order page', () => {
    const { el, start, status } = render({ token: null });

    expect(start).not.toHaveBeenCalled();
    expect(status).not.toHaveBeenCalled();
    expect(el.textContent).toContain('We could not find that payment');
    expect(el.querySelector('a[href="/track-order"]')).not.toBeNull();
    expect(el.querySelectorAll('h1').length).toBe(1);
  });

  describe('the return page', () => {
    it('PAY-10 coming back before the webhook shows confirming, never paid', () => {
      const { el, status } = render({ status: { status: 'ok', payment: payment('confirming') } });

      expect(status).toHaveBeenCalledWith('tok');
      const text = el.textContent ?? '';
      expect(text).not.toContain('Payment received');
      expect(el.querySelectorAll('h1').length).toBe(1);
    });

    it('PAY-1 a paid answer says so, with the order number and a way on', () => {
      const { fixture, el } = render({ status: { status: 'ok', payment: payment('paid') } });
      fixture.detectChanges();

      expect(el.querySelector('h1')?.textContent).toContain('Payment received');
      expect(el.textContent).toContain('RMP-2026-00001-4');
      expect(el.textContent).toContain('Rs. 3,150');
      expect(el.querySelector('a[href="/order-placed/RMP-2026-00001-4"]')).not.toBeNull();
    });

    it('PAY-2 a decline that can be retried offers another try, and says nothing was taken', () => {
      const { fixture, el } = render({ status: { status: 'ok', payment: payment('failed', true) } });
      fixture.detectChanges();

      expect(el.querySelector('h1')?.textContent).toContain('did not go through');
      expect(el.textContent).toContain('Nothing was taken');
      expect(Array.from(el.querySelectorAll('button')).some((b) => b.textContent?.includes('Try paying again'))).toBe(true);
    });

    it('PAY-3 a final failure offers no retry', () => {
      const { fixture, el } = render({ status: { status: 'ok', payment: payment('failed', false) } });
      fixture.detectChanges();

      expect(Array.from(el.querySelectorAll('button')).some((b) => b.textContent?.includes('Try paying again'))).toBe(false);
      expect(el.textContent).toContain('cancelled');
    });

    it('PAY-11 an expired payment says the time passed and nothing was charged', () => {
      const { fixture, el } = render({ status: { status: 'ok', payment: payment('expired') } });
      fixture.detectChanges();

      expect(el.querySelector('h1')?.textContent).toContain('time to pay has passed');
      expect(el.textContent).toContain('Nothing has been charged');
    });

    it('PAY-8 a payment under review says do not pay again', () => {
      const { fixture, el } = render({ status: { status: 'ok', payment: payment('review') } });
      fixture.detectChanges();

      expect(el.querySelector('h1')?.textContent).toContain('checking your payment');
      expect(el.textContent).toContain('do not pay again');
    });

    it('SEC-05 an invalid link says it could not be found, without detail', () => {
      const { fixture, el } = render({ status: { status: 'invalid' } });
      fixture.detectChanges();

      expect(el.querySelector('h1')?.textContent).toContain('could not find that payment');
      expect(el.querySelectorAll('h1').length).toBe(1);
    });

    it('a lost connection asks the shopper not to pay again', () => {
      const { fixture, el } = render({ status: { status: 'error' } });
      fixture.detectChanges();

      expect(el.textContent).toContain('do not pay again');
    });
  });

  describe('the pay page', () => {
    it('PAY-3 opening it asks the server for the hosted page once, with the token only', () => {
      const { start } = render({ mode: 'start', start: { status: 'ok', kind: 'Started', redirectUrl: null } });

      expect(start).toHaveBeenCalledTimes(1);
      expect(start).toHaveBeenCalledWith('tok');
    });

    it('PAY-3 while waiting it says so, with one heading', () => {
      const { el } = render({ mode: 'start', start: { status: 'ok', kind: 'Started', redirectUrl: null } });

      expect(el.querySelectorAll('h1').length).toBe(1);
    });

    it('PAY-7 when the gateway is unavailable it offers to try again, or cash on delivery from the bag', () => {
      const { fixture, el } = render({ mode: 'start', start: { status: 'ok', kind: 'GatewayUnavailable', redirectUrl: null } });
      fixture.detectChanges();

      expect(el.querySelector('h1')?.textContent).toContain('unavailable for a moment');
      expect(el.textContent).toContain('Cash on Delivery');
      expect(Array.from(el.querySelectorAll('button')).some((b) => b.textContent?.includes('Try again'))).toBe(true);
      expect(el.querySelector('a[href="/cart"]')).not.toBeNull();
    });

    it('PAY-11 when the time has passed it says the order was not kept', () => {
      const { fixture, el } = render({ mode: 'start', start: { status: 'ok', kind: 'WindowClosed', redirectUrl: null } });
      fixture.detectChanges();

      expect(el.querySelector('h1')?.textContent).toContain('can no longer be made');
      expect(el.textContent).toContain('Nothing has been charged');
    });

    it('PAY-8 a payment under review says not to pay again', () => {
      const { fixture, el } = render({ mode: 'start', start: { status: 'ok', kind: 'UnderReview', redirectUrl: null } });
      fixture.detectChanges();

      expect(el.textContent).toContain('do not pay again');
    });

    it('an error opening the page offers another try and says nothing was charged', () => {
      const { fixture, el } = render({ mode: 'start', start: { status: 'error' } });
      fixture.detectChanges();

      expect(el.textContent).toContain('Nothing has been charged');
      expect(Array.from(el.querySelectorAll('button')).some((b) => b.textContent?.includes('Try again'))).toBe(true);
    });
  });

  describe('where a shopper may be sent to pay', () => {
    it('SEC-10 allows a path on this shop or an https address', () => {
      expect(isSafePaymentUrl('/customer-api/orders/payments/fake/fk_1')).toBe(true);
      expect(isSafePaymentUrl('https://pay.example.com/session/abc')).toBe(true);
    });

    it('SEC-10 refuses anything else: other schemes, protocol-relative, plain http, empty', () => {
      for (const url of ['javascript:alert(1)', 'data:text/html,x', '//evil.example', 'http://pay.example.com', 'ftp://x', 'https://', '', '   ', null]) {
        expect(isSafePaymentUrl(url)).toBe(false);
      }
    });
  });
});
