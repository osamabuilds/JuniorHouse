import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { Address, AddressInput, AddressPage, ChangeResult, City, FieldErrors, LoadResult, Province } from '../models/address.model';

/** Same-origin pass-through to /api/customers/me/addresses (server.ts); the browser never calls the API directly (ADR 0021). */
const BOOK = '/customer-api/addresses';
const GEOGRAPHY = '/api/geography';

/** The names the API validates under, mapped to the names the form uses. */
const FIELD: Readonly<Record<string, string>> = {
  recipientname: 'recipientName', phone: 'phone', provinceid: 'provinceId', cityid: 'cityId', lineone: 'lineOne', linetwo: 'lineTwo', postcode: 'postcode',
};

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class AddressesApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<LoadResult> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<AddressPage>(BOOK, { params }).pipe(
      map((p): LoadResult => ({ status: 'ok', page: p })),
      catchError((e: unknown) => of<LoadResult>(e instanceof HttpErrorResponse && e.status === 401 ? { status: 'signedOut' } : { status: 'error' })),
    );
  }

  create(input: AddressInput): Observable<ChangeResult> {
    return this.change(this.http.post<Address>(BOOK, input));
  }

  update(id: number, input: AddressInput): Observable<ChangeResult> {
    return this.change(this.http.put<Address>(`${BOOK}/${id}`, input));
  }

  makeDefault(id: number): Observable<ChangeResult> {
    return this.change(this.http.post<Address>(`${BOOK}/${id}/default`, null));
  }

  remove(id: number): Observable<ChangeResult> {
    return this.change(this.http.delete<void>(`${BOOK}/${id}`));
  }

  provinces(): Observable<readonly Province[]> {
    return this.http.get<Province[]>(`${GEOGRAPHY}/provinces`).pipe(catchError(() => of<readonly Province[]>([])));
  }

  cities(provinceId: number): Observable<readonly City[]> {
    return this.http.get<City[]>(`${GEOGRAPHY}/provinces/${provinceId}/cities`).pipe(catchError(() => of<readonly City[]>([])));
  }

  private change(call: Observable<unknown>): Observable<ChangeResult> {
    return call.pipe(
      map((): ChangeResult => ({ status: 'ok' })),
      catchError((e: unknown) => of(toFailure(e))),
    );
  }
}

/** Maps a failed call to what the page can show. */
export function toFailure(e: unknown): ChangeResult {
  if (!(e instanceof HttpErrorResponse)) return { status: 'error' };
  switch (e.status) {
    case 400:
      return { status: 'invalid', errors: fieldErrors(e.error) };
    case 401:
      return { status: 'signedOut' };
    case 404:
      return { status: 'notFound' };
    case 409:
      return { status: 'refused', message: typeof e.error?.detail === 'string' ? e.error.detail : 'That could not be done.' };
    default:
      return { status: 'error' };
  }
}

function fieldErrors(body: unknown): FieldErrors {
  const errors = (body as { errors?: Record<string, string[] | string> } | null)?.errors;
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(errors ?? {})) {
    const message = Array.isArray(value) ? value[0] : value;
    if (typeof message === 'string') out[FIELD[key.toLowerCase()] ?? key] = message;
  }

  return out;
}
