import { createFeatureSelector, createSelector } from '@ngrx/store';
import { SESSION_FEATURE_KEY, SessionState } from './auth.state';

const selectSession = createFeatureSelector<SessionState>(SESSION_FEATURE_KEY);

export const selectSessionStatus = createSelector(selectSession, (state) => state.status);
export const selectSessionUser = createSelector(selectSession, (state) => state.user);
export const selectCsrfToken = createSelector(selectSessionUser, (user) => user?.csrfToken ?? null);
export const selectSigningIn = createSelector(selectSession, (state) => state.signingIn);
export const selectSessionSaving = createSelector(selectSession, (state) => state.saving);
export const selectSessionError = createSelector(selectSession, (state) => state.error);
export const selectMfaRequired = createSelector(selectSession, (state) => state.mfaRequired);
export const selectMfaEnded = createSelector(selectSession, (state) => state.mfaEnded);

/** The permissions the signed-in person holds right now; empty before anyone is known and for a session that owes a step. */
export const selectPermissions = createSelector(selectSessionUser, (user): readonly string[] => user?.permissions ?? []);

/** True when someone is signed in and holds at least one of the permissions (an empty list asks for none, so any signed-in person passes). */
export const holdsAny = (permissions: readonly string[], required: readonly string[]): boolean =>
  required.length === 0 || required.some((code) => permissions.includes(code));
