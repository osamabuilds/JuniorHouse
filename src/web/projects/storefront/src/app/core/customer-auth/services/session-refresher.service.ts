import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay } from 'rxjs';
import { AUTH_BASE } from '../models/customer-auth.model';

/**
 * Renews the customer cookies. Many requests can find the access cookie expired at the same moment; they all wait for one refresh call,
 * because rotating the refresh value twice would look like a stolen cookie and end the session. Browser use only (see the effects).
 */
@Injectable({ providedIn: 'root' })
export class SessionRefresher {
  private readonly http = inject(HttpClient);
  private inFlight: Observable<boolean> | null = null;

  /** Emits true when the cookies are renewed (or another tab just did it), false when the customer must sign in again. */
  refresh(): Observable<boolean> {
    this.inFlight ??= this.http.post<void>(`${AUTH_BASE}/refresh`, null, { observe: 'response' }).pipe(
      map(() => true),
      // 409: another tab rotated it a moment ago, so the browser already holds the new pair.
      catchError((e: { status?: number }) => of(e.status === 409)),
      finalize(() => (this.inFlight = null)),
      shareReplay(1),
    );
    return this.inFlight;
  }
}
