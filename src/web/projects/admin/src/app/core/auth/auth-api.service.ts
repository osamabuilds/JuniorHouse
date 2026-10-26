import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../http/api-client.service';
import { ChangePasswordValue, SessionUser, SignInResponse, SignInValue } from './auth.model';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly api = inject(ApiClient);

  me(): Observable<SessionUser> {
    return this.api.get<SessionUser>('/api/auth/me');
  }

  signIn(value: SignInValue): Observable<SignInResponse> {
    return this.api.post<SignInResponse>('/api/auth/sign-in', value);
  }

  signOut(): Observable<void> {
    return this.api.post<void>('/api/auth/sign-out', {});
  }

  signOutEverywhere(): Observable<void> {
    return this.api.post<void>('/api/auth/sign-out-all', {});
  }

  changePassword(value: ChangePasswordValue): Observable<void> {
    return this.api.post<void>('/api/auth/password', value);
  }
}
