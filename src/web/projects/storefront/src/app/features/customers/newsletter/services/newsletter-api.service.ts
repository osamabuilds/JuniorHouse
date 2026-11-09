import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { MineResult, SubscribeResult } from '../models/newsletter.model';

/** Same-origin pass-through to /api/newsletter and /api/customers/me/newsletter (server.ts); the browser never calls the API directly. */
const BASE = '/customer-api/newsletter';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class NewsletterApi {
  private readonly http = inject(HttpClient);

  /** The footer form. The answer is the same for a new, a repeated and a previously unsubscribed address (NLS-6). */
  subscribe(email: string): Observable<SubscribeResult> {
    return this.http.post<unknown>(`${BASE}/subscribe`, { email }).pipe(
      map((): SubscribeResult => 'ok'),
      catchError((e: unknown) => of<SubscribeResult>(subscribeFailure(e))),
    );
  }

  mine(): Observable<MineResult> {
    return this.http.get<{ subscribed: boolean }>(BASE).pipe(
      map((r): MineResult => ({ status: 'ok', subscribed: r.subscribed })),
      catchError((e: unknown) => of(mineFailure(e))),
    );
  }

  setMine(subscribed: boolean): Observable<MineResult> {
    return this.http.put<{ subscribed: boolean }>(BASE, { subscribed }).pipe(
      map((r): MineResult => ({ status: 'ok', subscribed: r.subscribed })),
      catchError((e: unknown) => of(mineFailure(e))),
    );
  }
}

function subscribeFailure(e: unknown): SubscribeResult {
  if (!(e instanceof HttpErrorResponse)) return 'error';
  if (e.status === 400 || e.status === 422) return 'invalid';
  if (e.status === 429) return 'busy';
  return 'error';
}

function mineFailure(e: unknown): MineResult {
  return e instanceof HttpErrorResponse && e.status === 401 ? { status: 'signedOut' } : { status: 'error' };
}
