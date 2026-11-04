import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { ChangeResult, IdsResult, ListResult, WishlistPage } from '../models/wishlist.model';

/** Same-origin pass-through to /api/customers/me/wishlist (server.ts); the browser never calls the API directly (ADR 0021). */
const BASE = '/customer-api/wishlist';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class WishlistApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<ListResult> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<WishlistPage>(BASE, { params }).pipe(
      map((p): ListResult => ({ status: 'ok', page: p })),
      catchError((e: unknown) => of<ListResult>(isUnauthorized(e) ? { status: 'signedOut' } : { status: 'error' })),
    );
  }

  ids(): Observable<IdsResult> {
    return this.http.get<number[]>(`${BASE}/ids`).pipe(
      map((ids): IdsResult => ({ status: 'ok', ids })),
      catchError((e: unknown) => of<IdsResult>(isUnauthorized(e) ? { status: 'signedOut' } : { status: 'error' })),
    );
  }

  add(productId: number): Observable<ChangeResult> {
    return this.change(this.http.put<void>(`${BASE}/${productId}`, null));
  }

  remove(productId: number): Observable<ChangeResult> {
    return this.change(this.http.delete<void>(`${BASE}/${productId}`));
  }

  /** Adds the products a guest tapped before signing in. Only ids go up; the server decides what is added (WSL-3). */
  merge(productIds: readonly number[]): Observable<ChangeResult> {
    return this.change(this.http.post<{ added: number }>(`${BASE}/merge`, { productIds }));
  }

  private change(call: Observable<unknown>): Observable<ChangeResult> {
    return call.pipe(
      map((): ChangeResult => ({ status: 'ok' })),
      catchError((e: unknown) => of(toFailure(e))),
    );
  }
}

function isUnauthorized(e: unknown): boolean {
  return e instanceof HttpErrorResponse && e.status === 401;
}

/** Maps a failed call to what the page can show. */
export function toFailure(e: unknown): ChangeResult {
  if (!(e instanceof HttpErrorResponse)) return { status: 'error' };
  switch (e.status) {
    case 401:
      return { status: 'signedOut' };
    case 404:
      return { status: 'notFound' };
    case 409:
    case 422:
      return { status: 'refused', message: typeof e.error?.detail === 'string' ? e.error.detail : 'That could not be done.' };
    default:
      return { status: 'error' };
  }
}
