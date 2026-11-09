import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { NotifyResult } from '../models/notify-me.model';

/** Same-origin pass-through to /api/notify-me (server.ts); the browser never calls the API directly. */
const ENDPOINT = '/customer-api/notify-me';

const GENERIC_INVALID = 'Please check the email address and try again.';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class NotifyMeApi {
  private readonly http = inject(HttpClient);

  /** Asks to be told when a sold-out size is back (NTM-2). The answer is the same for a new and a repeated address (NTM-3). */
  request(variantId: number, email: string): Observable<NotifyResult> {
    return this.http.post<unknown>(ENDPOINT, { variantId, email }).pipe(
      map((): NotifyResult => ({ status: 'ok' })),
      catchError((e: unknown) => of(failure(e))),
    );
  }
}

function failure(e: unknown): NotifyResult {
  if (!(e instanceof HttpErrorResponse)) return { status: 'error' };
  if (e.status === 429) return { status: 'busy' };
  if (e.status === 400 || e.status === 422) return { status: 'invalid', message: firstMessage(e.error) ?? GENERIC_INVALID };
  return { status: 'error' };
}

/** The first message in a problem-details `errors` object ({ field: [message] }), or null. */
function firstMessage(body: unknown): string | null {
  const errors = (body as { errors?: Record<string, unknown> } | null)?.errors;
  if (!errors || typeof errors !== 'object') return null;
  for (const value of Object.values(errors)) {
    if (Array.isArray(value) && typeof value[0] === 'string' && value[0].length > 0) return value[0];
  }
  return null;
}
