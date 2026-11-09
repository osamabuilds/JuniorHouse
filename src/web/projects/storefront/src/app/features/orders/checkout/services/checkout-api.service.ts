import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { CheckoutForm, City, FieldErrors, PlaceResult, Province, SummaryResult } from '../models/checkout.model';

/** Same-origin pass-through to /api/orders (server.ts): the cookies belong to this site and the browser never calls the API directly. */
const ORDERS = '/customer-api/orders';
const GEOGRAPHY = '/api/geography';

/** HTTP only. Every failure becomes a result: nothing throws into the store or the page. */
@Injectable({ providedIn: 'root' })
export class CheckoutApi {
  private readonly http = inject(HttpClient);

  summary(cityId: number | null, method: string): Observable<SummaryResult> {
    let params = new HttpParams().set('method', method.toLowerCase());
    if (cityId) params = params.set('cityId', cityId);
    return this.http.get<SummaryResultBody>(`${ORDERS}/checkout`, { params }).pipe(
      map((summary): SummaryResult => ({ status: 'ok', summary })),
      catchError(() => of<SummaryResult>({ status: 'error' })),
    );
  }

  provinces(): Observable<readonly Province[]> {
    return this.http.get<Province[]>(`${GEOGRAPHY}/provinces`).pipe(catchError(() => of<readonly Province[]>([])));
  }

  cities(provinceId: number): Observable<readonly City[]> {
    return this.http.get<City[]>(`${GEOGRAPHY}/provinces/${provinceId}/cities`).pipe(catchError(() => of<readonly City[]>([])));
  }

  /** Sends the typed details and the one key made for this page load. The body carries no price, total or quantity (SEC-09). */
  place(form: CheckoutForm, key: string): Observable<PlaceResult> {
    const saved = form.savedAddressId !== null;
    const body = {
      contact: { name: form.name.trim(), email: form.email.trim() || null, mobile: form.mobile.trim(), whatsAppOptIn: form.whatsAppOptIn },
      savedAddressId: form.savedAddressId,
      address: saved
        ? { note: form.note.trim() || null, provinceId: 0, cityId: 0 }
        : {
            recipientName: form.recipientName.trim(),
            recipientPhone: form.recipientPhone.trim(),
            lineOne: form.lineOne.trim(),
            lineTwo: form.lineTwo.trim() || null,
            provinceId: form.provinceId,
            cityId: form.cityId,
            postcode: form.postcode.trim() || null,
            note: form.note.trim() || null,
          },
      paymentMethod: form.paymentMethod,
    };
    return this.http.post<{ orderNumber: string; wasRepeat: boolean; confirmToken?: string | null; payToken?: string | null }>(ORDERS, body, { headers: { 'Idempotency-Key': key } }).pipe(
      map((r): PlaceResult => ({ status: 'placed', orderNumber: r.orderNumber, wasRepeat: r.wasRepeat, confirmToken: r.confirmToken ?? null, payToken: r.payToken ?? null })),
      catchError((e: unknown) => of(toFailure(e))),
    );
  }
}

type SummaryResultBody = Extract<SummaryResult, { status: 'ok' }>['summary'];

/** The server answered in words (409 refusal, 400 field messages); anything else is a plain error. */
export function toFailure(e: unknown): PlaceResult {
  if (e instanceof HttpErrorResponse) {
    if (e.status === 409 && e.error && typeof e.error === 'object') {
      const body = e.error as { reason?: unknown; messages?: unknown; shortVariantIds?: unknown };
      return {
        status: 'refused',
        reason: typeof body.reason === 'string' ? body.reason : 'Refused',
        messages: Array.isArray(body.messages) ? body.messages.filter((m): m is string => typeof m === 'string') : [],
        shortVariantIds: Array.isArray(body.shortVariantIds) ? body.shortVariantIds.filter((n): n is number => typeof n === 'number') : [],
      };
    }
    if (e.status === 400 && e.error && typeof e.error === 'object') {
      return { status: 'invalid', errors: fieldErrors(e.error as { errors?: Record<string, unknown> }), message: null };
    }
  }
  return { status: 'error' };
}

/** Server field names (`Contact.Mobile`, `Address.LineOne`, `cityId`) to the form's field names, first message each. */
function fieldErrors(problem: { errors?: Record<string, unknown> }): FieldErrors {
  const out: Record<string, string> = {};
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    if (Array.isArray(messages) && typeof messages[0] === 'string') out[toFormField(key)] = messages[0];
  }
  return out;
}

const FIELD_NAMES: Readonly<Record<string, string>> = {
  'contact.name': 'name',
  'contact.email': 'email',
  'contact.mobile': 'mobile',
  'address.recipientname': 'recipientName',
  'address.recipientphone': 'recipientPhone',
  'address.lineone': 'lineOne',
  'address.linetwo': 'lineTwo',
  'address.cityid': 'cityId',
  'address.provinceid': 'provinceId',
  'address.postcode': 'postcode',
  'address.note': 'note',
  cityid: 'cityId',
  paymentmethod: 'paymentMethod',
  idempotencykey: 'form',
};

function toFormField(serverName: string): string {
  return FIELD_NAMES[serverName.toLowerCase()] ?? 'form';
}
