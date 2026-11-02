import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { RegisterInput, RegisterResult, ResetResult, SessionResult, SignInInput, SignInResult, SimpleResult } from '../models/customer-auth.model';

export const CustomerAuthActions = createActionGroup({
  source: 'Customer Auth',
  events: {
    'Session Requested': emptyProps(),
    'Session Loaded': props<{ result: SessionResult }>(),
    /** A sign-in or reset page opened: forget what the last visit left behind. */
    'Form Opened': emptyProps(),
    'Sign In Submitted': props<{ input: SignInInput }>(),
    'Sign In Done': props<{ result: SignInResult }>(),
    'Register Submitted': props<{ input: RegisterInput }>(),
    'Register Done': props<{ result: RegisterResult }>(),
    'Sign Out Submitted': emptyProps(),
    'Sign Out Done': emptyProps(),
    'Reset Requested': props<{ email: string }>(),
    'Reset Request Done': props<{ result: SimpleResult }>(),
    'Reset Confirmed': props<{ token: string; newPassword: string }>(),
    'Reset Confirm Done': props<{ result: ResetResult }>(),
    'Verify Email Submitted': props<{ token: string }>(),
    'Verify Email Done': props<{ result: SimpleResult }>(),
    'Resend Verification Submitted': emptyProps(),
    'Resend Verification Done': props<{ result: SimpleResult }>(),
  },
});
