import { createReducer, on } from '@ngrx/store';
import { MfaApiActions, MfaPageActions } from './mfa.actions';
import { initialMfaState } from './mfa.state';

export const mfaReducer = createReducer(
  initialMfaState,

  // The store outlives the page, so opening it starts clean: nothing from an earlier visit (a secret, codes) is still held.
  on(MfaPageActions.opened, (state) => ({ ...initialMfaState, status: state.status, step: 'starting' as const })),
  on(MfaPageActions.restartClicked, (state) => ({ ...state, step: 'starting' as const, enrolment: null, error: null })),
  on(MfaPageActions.codeSubmitted, MfaPageActions.regenerateSubmitted, (state) => ({ ...state, saving: true, error: null })),
  on(MfaPageActions.codesSaved, (state) => ({ ...state, step: 'idle' as const, recoveryCodes: null, enrolment: null, error: null })),
  on(MfaPageActions.statusOpened, (state) => ({ ...state, step: 'idle' as const, recoveryCodes: null, error: null })),

  on(MfaApiActions.startSucceeded, (state, { enrolment }) => ({ ...state, step: 'scanning' as const, enrolment, error: null })),
  on(MfaApiActions.startFailed, (state, { error }) => ({ ...state, step: 'idle' as const, enrolment: null, error })),
  // The secret has done its job once the code is accepted: it is dropped, and only the recovery codes stay on screen.
  on(MfaApiActions.confirmSucceeded, (state, { codes }) => ({ ...state, step: 'codes' as const, enrolment: null, recoveryCodes: codes, saving: false, error: null })),
  on(MfaApiActions.confirmFailed, (state, { error }) => ({ ...state, saving: false, error })),
  on(MfaApiActions.statusLoaded, (state, { status }) => ({ ...state, status })),
  on(MfaApiActions.statusFailed, (state, { error }) => ({ ...state, error })),
  on(MfaApiActions.regenerateSucceeded, (state, { codes }) => ({ ...state, step: 'codes' as const, recoveryCodes: codes, saving: false, error: null })),
  on(MfaApiActions.regenerateFailed, (state, { error }) => ({ ...state, saving: false, error })),
);
