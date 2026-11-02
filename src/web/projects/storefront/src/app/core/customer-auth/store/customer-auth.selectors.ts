import { createSelector } from '@ngrx/store';
import { customerAuthFeature } from './customer-auth.reducer';

export const selectAuthStatus = customerAuthFeature.selectStatus;
export const selectAuthSession = customerAuthFeature.selectSession;
export const selectSignInForm = customerAuthFeature.selectSignIn;
export const selectRegisterForm = customerAuthFeature.selectRegister;
export const selectResetRequest = customerAuthFeature.selectResetRequest;
export const selectResetConfirm = customerAuthFeature.selectResetConfirm;
export const selectVerifyStatus = customerAuthFeature.selectVerify;
export const selectResendStatus = customerAuthFeature.selectResend;

/** The notice bar shows only for a signed-in customer whose email is not yet verified (ACT-31). */
export const selectShowVerifyNotice = createSelector(selectAuthStatus, selectAuthSession, (status, session) => status === 'signedIn' && session !== null && !session.emailVerified);
