import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { Cart, CartResult } from '../models/cart.model';

/** Same-origin pass-through to /api/cart (server.ts); the browser never calls the API directly, so the cookies belong to this site. */
const BASE = '/customer-api/cart';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class CartApi {
  private readonly http = inject(HttpClient);

  load(): Observable<CartResult> {
    return this.result(this.http.get<Cart>(BASE));
  }

  add(variantId: number, quantity: number): Observable<CartResult> {
    return this.result(this.http.post<Cart>(`${BASE}/lines`, { variantId, quantity }));
  }

  setQuantity(variantId: number, quantity: number): Observable<CartResult> {
    return this.result(this.http.put<Cart>(`${BASE}/lines/${variantId}`, { quantity }));
  }

  remove(variantId: number): Observable<CartResult> {
    return this.result(this.http.delete<Cart>(`${BASE}/lines/${variantId}`));
  }

  private result(call: Observable<Cart>): Observable<CartResult> {
    return call.pipe(
      map((cart): CartResult => ({ status: 'ok', cart })),
      catchError((e: unknown) => of(toFailure(e))),
    );
  }
}

/** A refusal the shopper can act on (out of stock, over the limit) keeps the server's words; anything else is a plain error. */
export function toFailure(e: unknown): CartResult {
  if (e instanceof HttpErrorResponse && (e.status === 400 || e.status === 409 || e.status === 422)) {
    const message = firstMessage(e.error);
    if (message) return { status: 'refused', message };
  }
  return { status: 'error' };
}

function firstMessage(body: unknown): string | null {
  if (!body || typeof body !== 'object') return null;
  const problem = body as { errors?: Record<string, unknown>; detail?: unknown; title?: unknown };
  for (const messages of Object.values(problem.errors ?? {})) {
    if (Array.isArray(messages) && typeof messages[0] === 'string') return messages[0];
  }
  if (typeof problem.detail === 'string' && problem.detail.length > 0) return problem.detail;
  return null;
}
