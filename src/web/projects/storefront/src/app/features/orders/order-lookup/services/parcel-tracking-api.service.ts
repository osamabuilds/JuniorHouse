import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { ParcelResult, ShipmentTracking } from '../models/shipment-tracking.model';

const URL = '/customer-api/shipping/track';

/** HTTP only. Every failure becomes a result: nothing throws into the page. The tracking number goes in the query, never in a path (TRK-9). */
@Injectable({ providedIn: 'root' })
export class ParcelTrackingApi {
  private readonly http = inject(HttpClient);

  track(waybill: string, captchaToken: string | null): Observable<ParcelResult> {
    let params = new HttpParams().set('waybill', waybill);
    if (captchaToken) params = params.set('captchaToken', captchaToken);
    return this.http.get<ShipmentTracking>(URL, { params }).pipe(
      map((tracking): ParcelResult => ({ kind: 'found', tracking })),
      catchError((error: unknown) => of(failure(error))),
    );
  }
}

function failure(error: unknown): ParcelResult {
  const status = error instanceof HttpErrorResponse ? error.status : 0;
  if (status === 404) return { kind: 'notfound' };
  if (status === 429) return { kind: 'limited' };
  // 428 here means "show the check". It is not a 401, which the session interceptor would take for an expired sign-in.
  if (status === 428) return { kind: 'captcha' };
  return { kind: 'error' };
}
