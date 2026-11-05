import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { LookupInput, LookupResult, TrackedOrder } from '../models/order-lookup.model';

const BASE = '/customer-api/orders/lookup';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class OrderLookupApi {
  private readonly http = inject(HttpClient);

  lookup(input: LookupInput): Observable<LookupResult> {
    return this.http.post<TrackedOrder>(BASE, { orderNumber: input.orderNumber, mobile: input.mobile, captchaToken: input.captchaToken }).pipe(
      map((order): LookupResult => ({ kind: 'found', order })),
      catchError((error: unknown) => of(failure(error))),
    );
  }

  /** The signed link from the order email (OLK-5). */
  byLink(token: string): Observable<LookupResult> {
    return this.http.get<TrackedOrder>(`${BASE}/link`, { params: new HttpParams().set('token', token) }).pipe(
      map((order): LookupResult => ({ kind: 'found', order })),
      catchError((error: unknown) => of(failure(error))),
    );
  }
}

function failure(error: unknown): LookupResult {
  const status = error instanceof HttpErrorResponse ? error.status : 0;
  if (status === 404) return { kind: 'mismatch' };
  if (status === 429) return { kind: 'limited' };
  // 428 on this route means "show the check". It is not a 401, which the session interceptor would take for an expired sign-in.
  if (status === 428) return { kind: 'captcha' };
  return { kind: 'error' };
}
