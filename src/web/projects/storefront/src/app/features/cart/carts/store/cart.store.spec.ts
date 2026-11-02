import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Observable, Subject, of } from 'rxjs';
import { Cart, CartLine, CartResult } from '../models/cart.model';
import { CartApi } from '../services/cart-api.service';
import { provideCart } from '../cart.providers';
import { CartActions } from './cart.actions';
import { selectCart, selectCartBusy, selectCartDrawerOpen, selectCartItemCount, selectCartMessage, selectCartStatus } from './cart.selectors';

const line = (variantId: number, quantity: number): CartLine => ({
  variantId, productId: 1, name: 'Dress', path: '/girls/dresses/dress', colour: 'Red', size: '2-3Y', sku: 'S', unitPrice: 1000, compareAtPrice: null,
  quantity, lineTotal: 1000 * quantity, available: 9, previousPrice: null, issues: [], imageKey: null, imageAlt: null, imageWidth: null, imageHeight: null,
});

const cartOf = (...lines: CartLine[]): Cart => ({ lines, itemCount: lines.reduce((n, l) => n + l.quantity, 0), subtotal: lines.reduce((n, l) => n + l.lineTotal, 0), hasProblems: false });

class FakeApi {
  load: () => Observable<CartResult> = () => of({ status: 'ok', cart: cartOf() });
  add: (v: number, q: number) => Observable<CartResult> = () => of({ status: 'ok', cart: cartOf() });
  setQuantity: (v: number, q: number) => Observable<CartResult> = () => of({ status: 'ok', cart: cartOf() });
  remove: (v: number) => Observable<CartResult> = () => of({ status: 'ok', cart: cartOf() });
}

function setUp(api = new FakeApi()) {
  TestBed.configureTestingModule({ providers: [provideStore(), provideEffects(), provideCart(), { provide: CartApi, useValue: api }] });
  return { api, store: TestBed.inject(Store) };
}

describe('cart store', () => {
  it('CRT-1 loading puts the server\'s basket in the store and the count on the header', () => {
    const { store } = setUp(Object.assign(new FakeApi(), { load: () => of<CartResult>({ status: 'ok', cart: cartOf(line(1, 2), line(2, 1)) }) }));

    store.dispatch(CartActions.load());

    expect(store.selectSignal(selectCartStatus)()).toBe('ready');
    expect(store.selectSignal(selectCartItemCount)()).toBe(3);
  });

  it('a failed first load is an error state, and a failed reload keeps the basket on screen', () => {
    const api = new FakeApi();
    const { store } = setUp(api);
    api.load = () => of({ status: 'error' });
    store.dispatch(CartActions.load());
    expect(store.selectSignal(selectCartStatus)()).toBe('error');

    api.load = () => of({ status: 'ok', cart: cartOf(line(1, 1)) });
    store.dispatch(CartActions.load());
    api.load = () => of({ status: 'error' });
    store.dispatch(CartActions.load());

    expect(store.selectSignal(selectCartStatus)()).toBe('ready');
    expect(store.selectSignal(selectCart)().lines).toHaveLength(1);
  });

  it('CRT-1 Add to bag updates the basket and opens the drawer', () => {
    const api = new FakeApi();
    api.add = () => of({ status: 'ok', cart: cartOf(line(1, 1)) });
    const { store } = setUp(api);

    store.dispatch(CartActions.add({ variantId: 1, quantity: 1 }));

    expect(store.selectSignal(selectCartItemCount)()).toBe(1);
    expect(store.selectSignal(selectCartDrawerOpen)()).toBe(true);
    expect(store.selectSignal(selectCartBusy)()).toBe(false);
  });

  it('a quantity change updates the basket without opening the drawer', () => {
    const api = new FakeApi();
    api.setQuantity = () => of({ status: 'ok', cart: cartOf(line(1, 4)) });
    const { store } = setUp(api);

    store.dispatch(CartActions.setQuantity({ variantId: 1, quantity: 4 }));

    expect(store.selectSignal(selectCartItemCount)()).toBe(4);
    expect(store.selectSignal(selectCartDrawerOpen)()).toBe(false);
  });

  it('CRT-4 a refusal shows the server\'s words, keeps the basket as it was and stays closed', () => {
    const api = new FakeApi();
    const { store } = setUp(api);
    api.add = () => of({ status: 'ok', cart: cartOf(line(1, 1)) });
    store.dispatch(CartActions.add({ variantId: 1, quantity: 1 }));
    store.dispatch(CartActions.closeDrawer());
    api.add = () => of({ status: 'refused', message: 'Only 1 is in stock.' });

    store.dispatch(CartActions.add({ variantId: 1, quantity: 1 }));

    expect(store.selectSignal(selectCartMessage)()).toBe('Only 1 is in stock.');
    expect(store.selectSignal(selectCartItemCount)()).toBe(1);
    expect(store.selectSignal(selectCartDrawerOpen)()).toBe(false);
  });

  it('a network error says so in plain words, and the next change clears the message', () => {
    const api = new FakeApi();
    api.remove = () => of({ status: 'error' });
    const { store } = setUp(api);

    store.dispatch(CartActions.remove({ variantId: 1 }));
    expect(store.selectSignal(selectCartMessage)()).toContain('Something went wrong');

    api.remove = () => of({ status: 'ok', cart: cartOf() });
    store.dispatch(CartActions.remove({ variantId: 1 }));
    expect(store.selectSignal(selectCartMessage)()).toBeNull();
  });

  it('the controls wait while a change is in flight, and changes run in the order they were made', () => {
    const first = new Subject<CartResult>();
    const order: string[] = [];
    const api = new FakeApi();
    api.setQuantity = (v, q) => {
      order.push(`set ${v}:${q}`);
      return v === 1 ? first : of<CartResult>({ status: 'ok', cart: cartOf(line(2, q)) });
    };
    const { store } = setUp(api);

    store.dispatch(CartActions.setQuantity({ variantId: 1, quantity: 2 }));
    store.dispatch(CartActions.setQuantity({ variantId: 2, quantity: 3 }));
    expect(store.selectSignal(selectCartBusy)()).toBe(true);
    expect(order).toEqual(['set 1:2']);

    first.next({ status: 'ok', cart: cartOf(line(1, 2)) });
    first.complete();

    expect(order).toEqual(['set 1:2', 'set 2:3']);
    expect(store.selectSignal(selectCartItemCount)()).toBe(3);
  });

  it('the drawer opens and closes by action', () => {
    const { store } = setUp();

    store.dispatch(CartActions.openDrawer());
    expect(store.selectSignal(selectCartDrawerOpen)()).toBe(true);
    store.dispatch(CartActions.closeDrawer());
    expect(store.selectSignal(selectCartDrawerOpen)()).toBe(false);
  });
});
