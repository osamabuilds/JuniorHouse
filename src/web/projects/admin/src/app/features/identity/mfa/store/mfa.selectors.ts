import { createFeatureSelector, createSelector } from '@ngrx/store';
import { MFA_FEATURE_KEY, MfaState } from './mfa.state';

const selectMfa = createFeatureSelector<MfaState>(MFA_FEATURE_KEY);

export const selectMfaStep = createSelector(selectMfa, (state) => state.step);
export const selectMfaEnrolment = createSelector(selectMfa, (state) => state.enrolment);
export const selectMfaCodes = createSelector(selectMfa, (state) => state.recoveryCodes);
export const selectMfaStatus = createSelector(selectMfa, (state) => state.status);
export const selectMfaSaving = createSelector(selectMfa, (state) => state.saving);
export const selectMfaError = createSelector(selectMfa, (state) => state.error);
