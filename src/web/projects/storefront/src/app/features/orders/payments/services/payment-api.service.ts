import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { PaymentStatus, StartKind, StartResult, StatusResult } from '../models/payment.model';

const BASE = '/customer-api/orders/payments';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class PaymentApi {
  private readonly http = inject(HttpClient);

  /** Opens (or reopens) the hosted payment page of the order the token names. Only a button or the pay page calls this. */
  start(token: string): Observable<StartResult> {
    return this.http.post<{ kind: StartKind; redirectUrl: string | null }>(`${BASE}/start`, { token }).pipe(
      map((r): StartResult => ({ status: 'ok', kind: r.kind, redirectUrl: r.redirectUrl ?? null })),
      catchError((e: unknown) => of(toStartFailure(e))),
    );
  }

  status(token: string): Observable<StatusResult> {
    return this.http.get<PaymentStatus>(`${BASE}/status`, { params: new HttpParams().set('token', token) }).pipe(
      map((payment): StatusResult => ({ status: 'ok', payment })),
      catchError((e: unknown) => of<StatusResult>(e instanceof HttpErrorResponse && e.status === 404 ? { status: 'invalid' } : { status: 'error' })),
    );
  }
}

/** 404 means the link is not one of ours; 503 and 409 carry the reason in words; anything else is a plain error. */
export function toStartFailure(e: unknown): StartResult {
  if (!(e instanceof HttpErrorResponse)) return { status: 'error' };
  if (e.status === 404) return { status: 'invalid' };
  const body = e.error as { kind?: StartKind } | null;
  return (e.status === 409 || e.status === 503) && body?.kind ? { status: 'ok', kind: body.kind, redirectUrl: null } : { status: 'error' };
}
