import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url.token';
import { toApiError } from './api-error.mapper';

type QueryParams = Record<string, string | number | boolean | undefined | null>;

/**
 * The one typed HTTP client every feature's API service is built on (SCRUM-174) - centralises the
 * base URL, query-string building, and turning a failed request into the shared {@link ApiError}
 * shape (never a raw HttpErrorResponse a template would have to know ASP.NET Core's ProblemDetails
 * format to read).
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  get<T>(path: string, params?: QueryParams): Observable<T> {
    return this.http
      .get<T>(`${this.baseUrl}${path}`, { params: this.buildParams(params) })
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => toApiError(error))));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .post<T>(`${this.baseUrl}${path}`, body)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => toApiError(error))));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .put<T>(`${this.baseUrl}${path}`, body)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => toApiError(error))));
  }

  delete<T>(path: string): Observable<T> {
    return this.http
      .delete<T>(`${this.baseUrl}${path}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => toApiError(error))));
  }

  /** Multipart upload (PO files). The browser sets the multipart boundary header itself. */
  postForm<T>(path: string, body: FormData): Observable<T> {
    return this.http
      .post<T>(`${this.baseUrl}${path}`, body)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => toApiError(error))));
  }

  /** Absolute URL for a plain link (e.g. a file download `<a href>`). */
  url(path: string): string {
    return `${this.baseUrl}${path}`;
  }

  private buildParams(params?: QueryParams): HttpParams {
    let httpParams = new HttpParams();

    for (const [key, value] of Object.entries(params ?? {})) {
      if (value !== undefined && value !== null && value !== '') {
        httpParams = httpParams.set(key, String(value));
      }
    }

    return httpParams;
  }
}
