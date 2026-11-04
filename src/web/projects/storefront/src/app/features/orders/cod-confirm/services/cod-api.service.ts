import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { CodAnswer, CodLinkStatus, CodOutcome, CodRespondResult, CodStatusResult } from '../models/cod.model';

const BASE = '/customer-api/orders/cod';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class CodApi {
  private readonly http = inject(HttpClient);

  status(token: string): Observable<CodStatusResult> {
    return this.http.get<CodLinkStatus>(`${BASE}/status`, { params: new HttpParams().set('token', token) }).pipe(
      map((link): CodStatusResult => ({ status: 'ok', link })),
      catchError(() => of<CodStatusResult>({ status: 'error' })),
    );
  }

  /** Only a button press calls this; opening the link never does. */
  respond(token: string, answer: CodAnswer): Observable<CodRespondResult> {
    return this.http.post<{ outcome: CodOutcome; orderNumber: string | null }>(`${BASE}/${answer}`, { token }).pipe(
      map((r): CodRespondResult => ({ status: 'ok', outcome: r.outcome, orderNumber: r.orderNumber })),
      catchError(() => of<CodRespondResult>({ status: 'error' })),
    );
  }
}
