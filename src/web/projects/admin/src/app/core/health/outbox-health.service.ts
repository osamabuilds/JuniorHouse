import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../http/api-client.service';

/** One module's outbox at a glance (GET /api/admin/outbox): what waits, what is being delivered, what is retrying, what gave up. */
export interface OutboxSchemaHealth {
  readonly schema: string;
  readonly pending: number;
  readonly inFlight: number;
  readonly retrying: number;
  readonly deadLettered: number;
  readonly oldestPendingSeconds: number | null;
}

@Injectable({ providedIn: 'root' })
export class OutboxHealthService {
  private readonly api = inject(ApiClient);

  read(): Observable<readonly OutboxSchemaHealth[]> {
    return this.api.get<readonly OutboxSchemaHealth[]>('/api/admin/outbox');
  }
}
