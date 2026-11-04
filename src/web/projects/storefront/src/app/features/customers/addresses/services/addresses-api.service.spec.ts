import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChangeResult, EMPTY_INPUT, LoadResult } from '../models/address.model';
import { AddressesApi } from './addresses-api.service';

describe('AddressesApi', () => {
  let api: AddressesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AddressesApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  function change(call: () => ReturnType<AddressesApi["create"]>, status: number, body: object): ChangeResult {
    let result!: ChangeResult;
    call().subscribe((r) => (result = r));
    http.expectOne(() => true).flush(body, { status, statusText: 'x' });
    return result;
  }

  it('ADD-7 asks for one page of the signed-in customers book through the same-origin route', () => {
    let result!: LoadResult;
    api.list(2, 10).subscribe((r) => (result = r));
    const req = http.expectOne((r) => r.url === '/customer-api/addresses');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({ items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0 });
    expect(result.status).toBe('ok');
  });

  it('SEC-05 never sends a customer id: add posts the typed fields only', () => {
    api.create({ ...EMPTY_INPUT, recipientName: 'Ayesha Khan' }).subscribe();
    const req = http.expectOne('/customer-api/addresses');
    expect(req.request.method).toBe('POST');
    expect(Object.keys(req.request.body as object).some((k) => /customer/i.test(k))).toBe(false);
    req.flush({});
  });

  it('ADD-3 a 401 on the list means signed out, and any other failure is an error result, not a throw', () => {
    let a!: LoadResult;
    api.list(1, 10).subscribe((r) => (a = r));
    http.expectOne((r) => r.url === '/customer-api/addresses').flush({}, { status: 401, statusText: 'x' });
    expect(a.status).toBe('signedOut');

    let b!: LoadResult;
    api.list(1, 10).subscribe((r) => (b = r));
    http.expectOne((r) => r.url === '/customer-api/addresses').flush({}, { status: 500, statusText: 'x' });
    expect(b.status).toBe('error');
  });

  it('ADD-4 ADD-5 a 400 becomes messages keyed by the form field names', () => {
    const r = change(() => api.create(EMPTY_INPUT), 400, { errors: { Phone: ['Enter a Pakistani phone number such as 0300 1234567.'], CityId: ['Choose a city that is in the selected province.'] } });
    expect(r).toEqual({ status: 'invalid', errors: { phone: 'Enter a Pakistani phone number such as 0300 1234567.', cityId: 'Choose a city that is in the selected province.' } });
  });

  it('ADD-6 a 409 carries the refusal message, 404 is not found, 401 signed out', () => {
    expect(change(() => api.create(EMPTY_INPUT), 409, { detail: 'You can save up to 10 addresses. Remove one to add another.' })).toEqual({
      status: 'refused',
      message: 'You can save up to 10 addresses. Remove one to add another.',
    });
    expect(change(() => api.remove(5), 404, {}).status).toBe('notFound');
    expect(change(() => api.makeDefault(5), 401, {}).status).toBe('signedOut');
  });

  it('removes with DELETE and makes default with POST on the address', () => {
    api.remove(7).subscribe();
    const del = http.expectOne('/customer-api/addresses/7');
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
    api.makeDefault(7).subscribe();
    const post = http.expectOne('/customer-api/addresses/7/default');
    expect(post.request.method).toBe('POST');
    post.flush({});
  });

  it('ADD-4 geography failures give empty lists, never an error', () => {
    let provinces: readonly unknown[] = [1];
    api.provinces().subscribe((p) => (provinces = p));
    http.expectOne('/api/geography/provinces').flush({}, { status: 500, statusText: 'x' });
    expect(provinces).toEqual([]);
  });
});
