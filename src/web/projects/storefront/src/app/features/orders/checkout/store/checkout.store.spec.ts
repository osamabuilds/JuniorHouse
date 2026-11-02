import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Observable, Subject, of } from 'rxjs';
import { provideCheckout } from '../checkout.providers';
import { CheckoutForm, CheckoutSummary, City, EMPTY_FORM, PlaceResult, Province, SummaryResult } from '../models/checkout.model';
import { CheckoutApi } from '../services/checkout-api.service';
import { CheckoutActions } from './checkout.actions';
import { selectCheckoutCities, selectCheckoutPlacing, selectCheckoutResult, selectCheckoutStatus, selectCheckoutSummary } from './checkout.reducer';

const summary = (shipping: number | null): CheckoutSummary => ({
  lines: [], subtotal: 2900, discount: 0, shipping, total: 2900 + (shipping ?? 0), canOrder: true, problems: [],
  paymentMethods: [{ code: 'COD', id: 1, name: 'Cash on Delivery', isOnline: false }], savedAddresses: [],
});

class FakeApi {
  summary: (c: number | null, m: string) => Observable<SummaryResult> = (c) => of({ status: 'ok', summary: summary(c ? 250 : null) });
  provinces = (): Observable<readonly Province[]> => of([{ id: 2, code: 'PB', name: 'Punjab' }]);
  cities = (): Observable<readonly City[]> => of([{ id: 7, code: 'LHE', name: 'Lahore', provinceId: 2 }]);
  place: (f: CheckoutForm, k: string) => Observable<PlaceResult> = () => of({ status: 'placed', orderNumber: 'RMP-2026-00001-4', wasRepeat: false });
}

function setUp(api = new FakeApi()) {
  TestBed.configureTestingModule({ providers: [provideStore(), provideEffects(), provideCheckout(), { provide: CheckoutApi, useValue: api }] });
  return { api, store: TestBed.inject(Store) };
}

const KEY = 'k'.repeat(32);

describe('checkout store', () => {
  it('CHK-9 opening loads the summary without a city, so delivery is still to be calculated', () => {
    const { store } = setUp();

    store.dispatch(CheckoutActions.opened());

    expect(store.selectSignal(selectCheckoutStatus)()).toBe('ready');
    expect(store.selectSignal(selectCheckoutSummary)()?.shipping).toBeNull();
  });

  it('CHK-9 choosing a city asks the server again and the fee appears', () => {
    const { store } = setUp();
    store.dispatch(CheckoutActions.opened());

    store.dispatch(CheckoutActions.summaryRequested({ cityId: 7, method: 'COD' }));

    expect(store.selectSignal(selectCheckoutSummary)()?.shipping).toBe(250);
  });

  it('CHK-8 choosing a province loads only its cities', () => {
    const { store } = setUp();

    store.dispatch(CheckoutActions.provinceChosen({ provinceId: 2 }));

    expect(store.selectSignal(selectCheckoutCities)().map((c) => c.name)).toEqual(['Lahore']);
  });

  it('CHK-10 while one order is on its way, another press is ignored', () => {
    const wait = new Subject<PlaceResult>();
    const api = new FakeApi();
    let sent = 0;
    api.place = () => {
      sent++;
      return wait;
    };
    const { store } = setUp(api);

    store.dispatch(CheckoutActions.submitted({ form: EMPTY_FORM, key: KEY }));
    store.dispatch(CheckoutActions.submitted({ form: EMPTY_FORM, key: KEY }));

    expect(sent).toBe(1);
    expect(store.selectSignal(selectCheckoutPlacing)()).toBe(true);
    wait.next({ status: 'placed', orderNumber: 'RMP-2026-00001-4', wasRepeat: false });
    wait.complete();
    expect(store.selectSignal(selectCheckoutPlacing)()).toBe(false);
    expect(store.selectSignal(selectCheckoutResult)()).toEqual({ status: 'placed', orderNumber: 'RMP-2026-00001-4', wasRepeat: false });
  });

  it('ORD-3 a refusal is kept for the page to show, and the bag figures stay', () => {
    const api = new FakeApi();
    api.place = () => of({ status: 'refused', reason: 'OutOfStock', messages: ['Only 1 left.'], shortVariantIds: [11] });
    const { store } = setUp(api);
    store.dispatch(CheckoutActions.opened());

    store.dispatch(CheckoutActions.submitted({ form: EMPTY_FORM, key: KEY }));

    expect(store.selectSignal(selectCheckoutResult)()?.status).toBe('refused');
    expect(store.selectSignal(selectCheckoutSummary)()).not.toBeNull();
  });
});
