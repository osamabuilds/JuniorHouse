import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { MfaEnrolment, MfaStatusDto, RecoveryCodesDto } from '../models';

@Injectable({ providedIn: 'root' })
export class MfaApiService {
  private readonly api = inject(ApiClient);

  start(): Observable<MfaEnrolment> {
    return this.api.post<MfaEnrolment>('/api/auth/mfa/enrol/start', {});
  }

  confirm(code: string): Observable<RecoveryCodesDto> {
    return this.api.post<RecoveryCodesDto>('/api/auth/mfa/enrol/confirm', { code });
  }

  status(): Observable<MfaStatusDto> {
    return this.api.get<MfaStatusDto>('/api/auth/mfa');
  }

  regenerate(code: string): Observable<RecoveryCodesDto> {
    return this.api.post<RecoveryCodesDto>('/api/auth/mfa/recovery-codes', { code });
  }
}
