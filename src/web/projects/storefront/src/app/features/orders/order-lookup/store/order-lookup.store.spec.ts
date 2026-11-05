import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Observable, Subject, of } from 'rxjs';
import { LookupInput, LookupResult, TrackedOrder } from '../models/order-lookup.model';
import { provideOrderLookup } from '../order-lookup.providers';
import { OrderLookupApi } from '../services/order-lookup-api.service';
import { OrderLookupActions } from './order-lookup.actions';
import { selectLookupCaptchaRequired, selectLookupLinkFailed, selectLookupOrder, selectLookupStatus } from './order-lookup.reducer';

const order: TrackedOrder = {
  orderNumber: 'RMP-2026-00001-4', statusCode: 'PNDC', statusName: 'Waiting for your confirmation', placedAt: '2026-11-05T10:00:00Z', paymentMethodCode: 'COD',
  maskedMobile: '03XX-XXX4567', city: 'Lahore', subtotal: 2900, discount: 0, shipping: 250, total: 3150, lines: [{ name: 'Floral Dress', colour: 'Red', size: '2-3Y', quantity: 2, lineTotal: 2400 }],
};
const input: LookupInput = { orderNumber: 'RMP-2026-00001-4', mobile: '03001234567', captchaToken: null };

class FakeApi {
  lookup: (i: LookupInput) => Observable<LookupResult> = () => of({ kind: 'found', order });
  byLink: (t: string) => Observable<LookupResult> = () => of({ kind: 'found', order });
}

function setUp(api = new FakeApi()) {
  TestBed.configureTestingModule({ providers: [provideStore(), provideEffects(), provideOrderLookup(), { provide: OrderLookupApi, useValue: api }] });
  return { store: TestBed.inject(Store) };
}

describe('order look-up store', () => {
  it('OLK-1 a match shows the order', () => {
    const { store } = setUp();

    store.dispatch(OrderLookupActions.lookupSubmitted({ input }));

    expect(store.selectSignal(selectLookupStatus)()).toBe('found');
    expect(store.selectSignal(selectLookupOrder)()?.orderNumber).toBe('RMP-2026-00001-4');
  });

  it('OLK-2 a mismatch is one state, whatever was wrong', () => {
    const api = new FakeApi();
    api.lookup = () => of({ kind: 'mismatch' });
    const { store } = setUp(api);

    store.dispatch(OrderLookupActions.lookupSubmitted({ input }));

    expect(store.selectSignal(selectLookupStatus)()).toBe('mismatch');
    expect(store.selectSignal(selectLookupOrder)()).toBeNull();
    expect(store.selectSignal(selectLookupCaptchaRequired)()).toBe(false);
  });

  it('OLK-4 when the server asks for the check, the page shows it and keeps showing it until a look-up works', () => {
    const api = new FakeApi();
    api.lookup = () => of({ kind: 'captcha' });
    const { store } = setUp(api);

    store.dispatch(OrderLookupActions.lookupSubmitted({ input }));
    expect(store.selectSignal(selectLookupCaptchaRequired)()).toBe(true);
    expect(store.selectSignal(selectLookupStatus)()).toBe('captcha');

    api.lookup = () => of({ kind: 'found', order });
    store.dispatch(OrderLookupActions.lookupSubmitted({ input: { ...input, captchaToken: 't' } }));
    expect(store.selectSignal(selectLookupStatus)()).toBe('found');
    expect(store.selectSignal(selectLookupCaptchaRequired)()).toBe(false);
  });

  it('OLK-3 a rate limit is its own state and keeps the check on', () => {
    const api = new FakeApi();
    api.lookup = () => of({ kind: 'limited' });
    const { store } = setUp(api);

    store.dispatch(OrderLookupActions.lookupSubmitted({ input }));

    expect(store.selectSignal(selectLookupStatus)()).toBe('limited');
    expect(store.selectSignal(selectLookupCaptchaRequired)()).toBe(true);
  });

  it('a second press while one look-up is on its way is ignored', () => {
    const wait = new Subject<LookupResult>();
    let sent = 0;
    const api = new FakeApi();
    api.lookup = () => {
      sent++;
      return wait;
    };
    const { store } = setUp(api);

    store.dispatch(OrderLookupActions.lookupSubmitted({ input }));
    store.dispatch(OrderLookupActions.lookupSubmitted({ input }));

    expect(sent).toBe(1);
    expect(store.selectSignal(selectLookupStatus)()).toBe('busy');
  });

  it('OLK-5 the emailed link opens the order with no typing, and a broken link says so', () => {
    const api = new FakeApi();
    const { store } = setUp(api);

    store.dispatch(OrderLookupActions.linkOpened({ token: 't' }));
    expect(store.selectSignal(selectLookupStatus)()).toBe('found');

    api.byLink = () => of({ kind: 'mismatch' });
    store.dispatch(OrderLookupActions.linkOpened({ token: 'bad' }));
    expect(store.selectSignal(selectLookupOrder)()).toBeNull();
    expect(store.selectSignal(selectLookupLinkFailed)()).toBe(true);
  });

  it('looking up another order starts from an empty form', () => {
    const { store } = setUp();
    store.dispatch(OrderLookupActions.lookupSubmitted({ input }));

    store.dispatch(OrderLookupActions.cleared());

    expect(store.selectSignal(selectLookupStatus)()).toBe('idle');
    expect(store.selectSignal(selectLookupOrder)()).toBeNull();
  });
});
