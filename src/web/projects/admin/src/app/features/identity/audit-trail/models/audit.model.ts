/** One audit entry (GET /api/identity/audit). It names the changed columns and never carries a value. */
export interface AuditEntryDto {
  readonly id: number;
  readonly occurredAt: string;
  readonly module: string;
  readonly entityType: string;
  readonly entityKey: string;
  readonly operation: 'INSERT' | 'UPDATE' | 'DELETE';
  readonly actor: string;
  readonly correlationId: string | null;
  readonly columns: readonly string[];
}

/** What the filter form holds. Dates are the day picked (yyyy-mm-dd) or empty. */
export interface AuditFormValue {
  readonly actor: string;
  readonly module: string;
  readonly entityType: string;
  readonly entityKey: string;
  readonly operation: string;
  readonly fromDate: string;
  readonly toDate: string;
}

export interface AuditFilters extends AuditFormValue {
  readonly page: number;
  readonly pageSize: number;
}

export const EMPTY_AUDIT_FORM: AuditFormValue = { actor: '', module: '', entityType: '', entityKey: '', operation: '', fromDate: '', toDate: '' };
