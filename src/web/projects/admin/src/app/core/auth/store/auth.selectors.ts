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
