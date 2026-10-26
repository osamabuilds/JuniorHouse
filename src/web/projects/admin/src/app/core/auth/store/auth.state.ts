import { ApiError } from '../../http/api-error.model';
import { SessionUser } from '../auth.model';

export const SESSION_FEATURE_KEY = 'session';

/** unknown until the first /me answer; then signed in or not. */
export type SessionStatus = 'unknown' | 'anonymous' | 'authenticated';

export interface SessionState {
  readonly status: SessionStatus;
  /** In memory only: nothing here is ever written to browser storage (sessions AC-13). */
  readonly user: SessionUser | null;
  readonly signingIn: boolean;
  readonly saving: boolean;
  readonly error: ApiError | null;
  /** The password was accepted but a second step is owed; the second step does not exist yet. */
  readonly mfaRequired: boolean;
}

export const initialSessionState: SessionState = {
  status: 'unknown',
  user: null,
  signingIn: false,
  saving: false,
  error: null,
  mfaRequired: false,
};
