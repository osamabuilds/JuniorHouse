import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { RoleMatrixDto } from '../models';

@Injectable({ providedIn: 'root' })
export class RolesApiService {
  private readonly api = inject(ApiClient);

  matrix(): Observable<RoleMatrixDto> {
    return this.api.get<RoleMatrixDto>('/api/identity/roles/matrix');
  }
}
