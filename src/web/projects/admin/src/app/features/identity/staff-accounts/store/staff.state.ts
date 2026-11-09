import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { RoleDto, StaffDto, TemporaryPasswordDto } from '../models';

export const STAFF_FEATURE_KEY = 'staffAccounts';

export type StaffMode = 'list' | 'create' | 'detail';

export interface StaffState {
  readonly staff: readonly StaffDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly mode: StaffMode;
  readonly selected: StaffDto | null;
  readonly roles: readonly RoleDto[];
  readonly saving: boolean;
  readonly searchText: string;
  readonly activeOnly: boolean;
  /** Held in memory only until the person confirms they have copied it (AC-4); never persisted or logged. */
  readonly oneTimePassword: TemporaryPasswordDto | null;
}

export const initialStaffState: StaffState = {
  staff: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  mode: 'list',
  selected: null,
  roles: [],
  saving: false,
  searchText: '',
  activeOnly: false,
  oneTimePassword: null,
};
