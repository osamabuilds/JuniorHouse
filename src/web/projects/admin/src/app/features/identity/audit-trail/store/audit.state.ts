import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { AuditEntryDto, AuditFormValue, EMPTY_AUDIT_FORM } from '../models';

export const AUDIT_FEATURE_KEY = 'auditTrail';

export interface AuditState {
  readonly entries: readonly AuditEntryDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly form: AuditFormValue;
  readonly selected: AuditEntryDto | null;
  readonly loading: boolean;
  readonly error: ApiError | null;
}

export const initialAuditState: AuditState = {
  entries: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  form: EMPTY_AUDIT_FORM,
  selected: null,
  loading: false,
  error: null,
};
