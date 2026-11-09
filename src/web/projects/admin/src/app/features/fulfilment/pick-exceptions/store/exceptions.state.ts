import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { ExceptionRow } from '../models';

export const EXCEPTIONS_FEATURE_KEY = 'pickExceptions';

export interface ExceptionsState {
  readonly rows: readonly ExceptionRow[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly openOnly: boolean;
  readonly loading: boolean;
  /** The exception being resolved, so its buttons wait. */
  readonly resolvingId: number | null;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialExceptionsState: ExceptionsState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  openOnly: true,
  loading: false,
  resolvingId: null,
  error: null,
  notice: null,
};
