import { createReducer, on } from '@ngrx/store';
import { SessionApiActions, SessionPageActions } from './auth.actions';
import { initialSessionState } from './auth.state';

export const sessionReducer = createReducer(
  initialSessionState,

  on(SessionPageActions.signInOpened, (state) => ({ ...state, error: null, mfaRequired: false, mfaEnded: false })),
  on(SessionPageActions.signInSubmitted, (state) => ({ ...state, signingIn: true, error: null, mfaRequired: false, mfaEnded: false })),
  on(SessionPageActions.mfaCodeSubmitted, (state) => ({ ...state, signingIn: true, error: null })),
  on(SessionPageActions.mfaCancelled, (state) => ({ ...state, mfaRequired: false, error: null })),
  on(SessionPageActions.changePasswordSubmitted, (state) => ({ ...state, saving: true, error: null })),
  on(SessionPageActions.sessionExpired, SessionApiActions.signedOut, () => ({ ...initialSessionState, status: 'anonymous' as const })),

  on(SessionApiActions.loadSucceeded, (state, { user }) => ({ ...state, status: 'authenticated' as const, user, signingIn: false, saving: false, error: null, mfaRequired: false })),
  on(SessionApiActions.loadFailed, (state) => ({ ...state, status: 'anonymous' as const, user: null, signingIn: false })),
  // A limited session is already started: stay "signing in" until /me says who the person is. A second step leaves the session unstarted.
  on(SessionApiActions.signInSucceeded, (state, { response }) => ({ ...state, signingIn: response.status === 'limited', mfaRequired: response.status === 'mfa_required' })),
  // The code was valid and a session exists; stay "signing in" until /me says who the person is.
  on(SessionApiActions.mfaVerified, (state) => ({ ...state, signingIn: true })),
  // A 401 titled "Sign in again" means the pending sign-in itself is gone, so the password step starts over; any other failure is a wrong code.
  on(SessionApiActions.mfaFailed, (state, { error }) =>
    error.status === 401 && error.message === 'Sign in again'
      ? { ...state, signingIn: false, mfaRequired: false, mfaEnded: true, error: null }
      : { ...state, signingIn: false, error },
  ),
  on(SessionApiActions.signInFailed, (state, { error }) => ({ ...state, signingIn: false, error })),
  on(SessionApiActions.passwordChangeFailed, (state, { error }) => ({ ...state, saving: false, error })),
);
