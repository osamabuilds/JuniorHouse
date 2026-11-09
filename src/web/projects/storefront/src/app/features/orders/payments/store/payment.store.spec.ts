import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Observable, Subject, of } from 'rxjs';
import { afterEach, vi } from 'vitest';
import { PaymentState, PaymentStatus, StartResult, StatusResult } from '../models/payment.model';
import { providePayments } from '../payment.providers';
import { PaymentApi } from '../services/payment-api.service';
import { PaymentActions } from './payment.actions';
import { MAX_POLLS, POLL_EVERY_MS } from './payment.effects';
import { selectPaymentPayment, selectPaymentPolls, selectPaymentStart, selectPaymentStarting, selectPaymentStatus } from './payment.reducer';

const payment = (state: PaymentState, canRetry = false): PaymentStatus => ({ state, orderNumber: 'RMP-2026-00001-4', total: 3150, canRetry, methodCode: 'CARD' });

class FakeApi {
  startCalls = 0;
  statusCalls = 0;
  start: (t: string) => Observable<StartResult> = () => {
    this.startCalls++;
    return of({ status: 'ok', kind: 'Started', redirectUrl: '/pay' });
  };
  status: (t: string) => Observable<StatusResult> = () => {
    this.statusCalls++;
    return of({ status: 'ok', payment: payment('paid') });
  };
}

function setUp(api = new FakeApi()) {
  TestBed.configureTestingModule({ providers: [provideStore(), provideEffects(), providePayments(), { provide: PaymentApi, useValue: api }] });
  return { store: TestBed.inject(Store), api };
}

describe('payment store', () => {
  afterEach(() => vi.useRealTimers());

  it('PAY-3 starting records the redirect the server opened', () => {
    const { store } = setUp();

    store.dispatch(PaymentActions.startRequested({ token: 't' }));

    expect(store.selectSignal(selectPaymentStart)()).toEqual({ status: 'ok', kind: 'Started', redirectUrl: '/pay' });
    expect(store.selectSignal(selectPaymentStarting)()).toBe(false);
  });

  it('PAY-3 a second press while the first is on its way is ignored: one page per order', () => {
    const wait = new Subject<StartResult>();
    const api = new FakeApi();
    api.start = () => {
      api.startCalls++;
      return wait;
    };
    const { store } = setUp(api);

    store.dispatch(PaymentActions.startRequested({ token: 't' }));
    store.dispatch(PaymentActions.startRequested({ token: 't' }));

    expect(api.startCalls).toBe(1);
    expect(store.selectSignal(selectPaymentStarting)()).toBe(true);
  });

  it('PAY-10 a paid answer settles the page and stops the asking', () => {
    vi.useFakeTimers();
    const { store, api } = setUp();

    store.dispatch(PaymentActions.statusRequested({ token: 't' }));
    vi.advanceTimersByTime(POLL_EVERY_MS * 5);

    expect(store.selectSignal(selectPaymentStatus)()).toBe('ready');
    expect(store.selectSignal(selectPaymentPayment)()?.state).toBe('paid');
    expect(api.statusCalls).toBe(1);
  });

  it('PAY-10 while confirming it keeps asking every few seconds, then stops when the answer changes', () => {
    vi.useFakeTimers();
    const api = new FakeApi();
    const answers: PaymentState[] = ['confirming', 'confirming', 'paid'];
    api.status = () => {
      api.statusCalls++;
      return of({ status: 'ok', payment: payment(answers.shift() ?? 'paid') });
    };
    const { store } = setUp(api);

    store.dispatch(PaymentActions.statusRequested({ token: 't' }));
    vi.advanceTimersByTime(0);
    expect(store.selectSignal(selectPaymentPayment)()?.state).toBe('confirming');
    vi.advanceTimersByTime(POLL_EVERY_MS);
    expect(store.selectSignal(selectPaymentPayment)()?.state).toBe('confirming');
    vi.advanceTimersByTime(POLL_EVERY_MS);
    expect(store.selectSignal(selectPaymentPayment)()?.state).toBe('paid');
    vi.advanceTimersByTime(POLL_EVERY_MS * 10);

    expect(api.statusCalls).toBe(3);
  });

  it('PAY-10 a payment that stays confirming is asked about a limited number of times, then the page can say so', () => {
    vi.useFakeTimers();
    const api = new FakeApi();
    api.status = () => {
      api.statusCalls++;
      return of({ status: 'ok', payment: payment('confirming') });
    };
    const { store } = setUp(api);

    store.dispatch(PaymentActions.statusRequested({ token: 't' }));
    vi.advanceTimersByTime(POLL_EVERY_MS * (MAX_POLLS + 20));

    expect(api.statusCalls).toBe(MAX_POLLS);
    expect(store.selectSignal(selectPaymentPolls)()).toBe(MAX_POLLS);
  });

  it('leaving the page ends the asking', () => {
    vi.useFakeTimers();
    const api = new FakeApi();
    api.status = () => {
      api.statusCalls++;
      return of({ status: 'ok', payment: payment('confirming') });
    };
    const { store } = setUp(api);
    store.dispatch(PaymentActions.statusRequested({ token: 't' }));
    vi.advanceTimersByTime(POLL_EVERY_MS);
    const before = api.statusCalls;

    store.dispatch(PaymentActions.reset());
    vi.advanceTimersByTime(POLL_EVERY_MS * 10);

    expect(api.statusCalls).toBe(before);
    expect(store.selectSignal(selectPaymentStatus)()).toBe('idle');
  });

  it('an invalid link is its own state', () => {
    const api = new FakeApi();
    api.status = () => of({ status: 'invalid' });
    vi.useFakeTimers();
    const first = setUp(api);
    first.store.dispatch(PaymentActions.statusRequested({ token: 'x' }));
    vi.advanceTimersByTime(1);
    expect(first.store.selectSignal(selectPaymentStatus)()).toBe('invalid');
  });

  it('a decline that can be retried is carried to the page as it is', () => {
    const api = new FakeApi();
    api.status = () => of({ status: 'ok', payment: payment('failed', true) });
    vi.useFakeTimers();
    const { store } = setUp(api);

    store.dispatch(PaymentActions.statusRequested({ token: 't' }));
    vi.advanceTimersByTime(1);

    expect(store.selectSignal(selectPaymentPayment)()).toEqual(payment('failed', true));
  });
});
