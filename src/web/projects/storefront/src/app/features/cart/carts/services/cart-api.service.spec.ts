import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Cart, CartResult } from '../models/cart.model';
import { CartApi } from './cart-api.service';

const cart: Cart = { lines: [], itemCount: 0, subtotal: 0, hasProblems: false, discount: 0, delivery: null, total: 0, discountCode: null, notices: [] };

describe('CartApi', () => {
  let api: CartApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CartApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function result(call: ReturnType<CartApi['load']>): { value?: CartResult } {
    const out: { value?: CartResult } = {};
    call.subscribe((r) => (out.value = r));
    return out;
  }

  it('CRT-1 reads the basket from the same-origin pass-through', () => {
    const out = result(api.load());
    const req = http.expectOne('/customer-api/cart');
    expect(req.request.method).toBe('GET');
    req.flush(cart);

    expect(out.value).toEqual({ status: 'ok', cart });
  });

  it('CRT-6 an add sends the variant and the quantity and nothing else', () => {
    result(api.add(7, 2));
    const req = http.expectOne('/customer-api/cart/lines');

    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ variantId: 7, quantity: 2 });
    req.flush(cart);
  });

  it('CRT-1 changing a quantity is a PUT and removing is a DELETE on the line', () => {
    result(api.setQuantity(7, 4));
    const put = http.expectOne('/customer-api/cart/lines/7');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ quantity: 4 });
    put.flush(cart);

    result(api.remove(7));
    const del = http.expectOne('/customer-api/cart/lines/7');
    expect(del.request.method).toBe('DELETE');
    del.flush(cart);
  });

  it('CRT-4 a refusal keeps the server\'s words so the shopper can act on them', () => {
    const out = result(api.add(7, 9));
    http.expectOne('/customer-api/cart/lines').flush({ errors: { quantity: ['Only 3 are in stock.'] } }, { status: 400, statusText: 'Bad Request' });

    expect(out.value).toEqual({ status: 'refused', message: 'Only 3 are in stock.' });
  });

  it('a conflict with a detail is also a refusal', () => {
    const out = result(api.setQuantity(7, 99));
    http.expectOne('/customer-api/cart/lines/7').flush({ detail: 'You can buy at most 10 of one item in a single order.' }, { status: 409, statusText: 'Conflict' });

    expect(out.value).toEqual({ status: 'refused', message: 'You can buy at most 10 of one item in a single order.' });
  });

  it('a failure with no message to show becomes a plain error and never throws', () => {
    const out = result(api.load());
    http.expectOne('/customer-api/cart').flush('boom', { status: 500, statusText: 'Server Error' });

    expect(out.value).toEqual({ status: 'error' });
  });
});
