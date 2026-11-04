import { ApiError } from '@core/http';
import { MfaEnrolment, MfaStatusDto } from '../models';

export const MFA_FEATURE_KEY = 'mfa';

/** idle: nothing to show; starting/scanning: the secret is being made or shown; codes: the recovery codes are on screen. */
export type MfaStep = 'idle' | 'starting' | 'scanning' | 'codes';

export interface MfaState {
  readonly step: MfaStep;
  /** The secret, in memory only while it is being scanned; dropped as soon as it has served (mfa AC-2, AC-7). */
  readonly enrolment: MfaEnrolment | null;
  /** Shown once; dropped when the person says they saved them (mfa AC-6). Never persisted. */
  readonly recoveryCodes: readonly string[] | null;
  readonly status: MfaStatusDto | null;
  readonly saving: boolean;
  readonly error: ApiError | null;
}

export const initialMfaState: MfaState = {
  step: 'idle',
  enrolment: null,
  recoveryCodes: null,
  status: null,
  saving: false,
  error: null,
};
