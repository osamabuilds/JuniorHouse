import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChangeResult, IdsResult, ListResult } from '../models/wishlist.model';
import { WishlistApi } from './wishlist-api.service';

describe('WishlistApi', () => {
  let api: WishlistApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(WishlistApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  function change(call: () => ReturnType<WishlistApi['add']>, status: number, body: object | null = null): ChangeResult {
    let result!: ChangeResult;
    call().subscribe((r) => (result = r));
    http.expectOne(() => true).flush(body, { status, statusText: 'x' });
    return result;
  }

  it('WSL-8 asks for one page of the signed-in customers list through the same-origin route', () => {
    let result!: ListResult;
    api.list(2, 24).subscribe((r) => (result = r));
    const req = http.expectOne((r) => r.url === '/customer-api/wishlist');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('24');
    req.flush({ items: [], page: 2, pageSize: 24, totalCount: 30, totalPages: 2 });

    expect(result.status).toBe('ok');
  });

  it('WSL-7 a 401 on the list or the ids means signed out, not an error', () => {
    let list!: ListResult;
    let ids!: IdsResult;
    api.list(1, 24).subscribe((r) => (list = r));
    http.expectOne('/customer-api/wishlist?page=1&pageSize=24').flush(null, { status: 401, statusText: 'x' });
    api.ids().subscribe((r) => (ids = r));
    http.expectOne('/customer-api/wishlist/ids').flush(null, { status: 401, statusText: 'x' });

    expect(list).toEqual({ status: 'signedOut' });
    expect(ids).toEqual({ status: 'signedOut' });
  });

  it('WSL-1 adds with PUT and removes with DELETE by product id', () => {
    api.add(42).subscribe();
    const put = http.expectOne('/customer-api/wishlist/42');
    expect(put.request.method).toBe('PUT');
    put.flush(null, { status: 204, statusText: 'No Content' });

    api.remove(42).subscribe();
    const del = http.expectOne('/customer-api/wishlist/42');
    expect(del.request.method).toBe('DELETE');
    del.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('WSL-3 merges the remembered ids as a list of ids and nothing else', () => {
    api.merge([1, 2]).subscribe();
    const req = http.expectOne('/customer-api/wishlist/merge');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ productIds: [1, 2] });
    req.flush({ added: 2 });
  });

  it('maps failures to results the page can show', () => {
    expect(change(() => api.add(1), 204)).toEqual({ status: 'ok' });
    expect(change(() => api.add(1), 401)).toEqual({ status: 'signedOut' });
    expect(change(() => api.remove(1), 404)).toEqual({ status: 'notFound' });
    expect(change(() => api.add(1), 409, { detail: 'Your wishlist is full.' })).toEqual({ status: 'refused', message: 'Your wishlist is full.' });
    expect(change(() => api.add(1), 500)).toEqual({ status: 'error' });
  });
});
