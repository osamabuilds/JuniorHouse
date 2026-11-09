import { ApiError } from '@core/http';
import { RoleMatrixDto } from '../models';

export const ROLES_FEATURE_KEY = 'roles';

export interface RolesState {
  readonly matrix: RoleMatrixDto | null;
  readonly loading: boolean;
  readonly error: ApiError | null;
}

export const initialRolesState: RolesState = { matrix: null, loading: false, error: null };
