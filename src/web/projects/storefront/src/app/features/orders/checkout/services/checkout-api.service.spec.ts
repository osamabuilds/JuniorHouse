import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { EMPTY_FORM, PlaceResult } from '../models/checkout.model';
import { CheckoutApi } from './checkout-api.service';

describe('CheckoutApi', () => {
  let api: CheckoutApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CheckoutApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const typed = { ...EMPTY_FORM, name: ' Ayesha ', mobile: '0300 1234567', recipientName: 'Ayesha', recipientPhone: '0300 1234567', lineOne: 'House 4', provinceId: 2, cityId: 7 };
  const key = 'k'.repeat(32);

  function placeWith(status: number, body: object): PlaceResult {
    let result!: PlaceResult;
    api.place(typed, key).subscribe((r) => (result = r));
    http.expectOne('/customer-api/orders').flush(body, { status, statusText: 'x' });
    return result;
  }

  it('SEC-09 sends the typed details and the key only: no price, total, discount or quantity', () => {
    api.place(typed, key).subscribe();
    const req = http.expectOne('/customer-api/orders');

    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Idempotency-Key')).toBe(key);
    expect(/price|total|discount|quantity|amount/i.test(JSON.stringify(req.request.body))).toBe(false);
    expect((req.request.body as { contact: { name: string } }).contact.name).toBe('Ayesha');
    req.flush({ orderNumber: 'RMP-2026-00001-4', wasRepeat: false });
  });

  it('SEC-05 a saved address sends its id and no typed address fields', () => {
    api.place({ ...typed, savedAddressId: 5 }, key).subscribe();
    const req = http.expectOne('/customer-api/orders');

    const body = req.request.body as { savedAddressId: number; address: Record<string, unknown> };
    expect(body.savedAddressId).toBe(5);
    expect(body.address['lineOne']).toBeUndefined();
    req.flush({ orderNumber: 'RMP-2026-00001-4', wasRepeat: false });
  });

  it('CHK-1 a created order is a placed result with its number', () => {
    expect(placeWith(201, { orderNumber: 'RMP-2026-00001-4', wasRepeat: false })).toEqual({ status: 'placed', orderNumber: 'RMP-2026-00001-4', wasRepeat: false });
  });

  it('ORD-3 a 409 carries the reason, the words and the short variants', () => {
    const result = placeWith(409, { reason: 'OutOfStock', messages: ['Only 1 of Dress are left.'], shortVariantIds: [11] });

    expect(result).toEqual({ status: 'refused', reason: 'OutOfStock', messages: ['Only 1 of Dress are left.'], shortVariantIds: [11] });
  });

  it('CHK-2 CHK-3 a 400 becomes messages keyed by the form field names', () => {
    const result = placeWith(400, { errors: { 'Contact.Mobile': ['Enter a Pakistani mobile number such as 0300 1234567.'], cityId: ['Choose a city that belongs to the province.'] } });

    expect(result).toEqual({
      status: 'invalid',
      errors: { mobile: 'Enter a Pakistani mobile number such as 0300 1234567.', cityId: 'Choose a city that belongs to the province.' },
      message: null,
    });
  });

  it('any other failure is a plain error result, not a throw', () => {
    expect(placeWith(500, {})).toEqual({ status: 'error' });
  });

  it('CHK-9 the summary asks for the chosen city and method', () => {
    api.summary(7, 'COD').subscribe();
    const req = http.expectOne((r) => r.url === '/customer-api/orders/checkout');

    expect(req.request.params.get('cityId')).toBe('7');
    expect(req.request.params.get('method')).toBe('cod');
    req.flush({});
  });
});
