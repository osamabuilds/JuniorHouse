import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '../../http/api-error.model';
import { ChangePasswordValue, SessionUser, SignInResponse, SignInValue } from '../auth.model';

export const SessionPageActions = createActionGroup({
  source: 'Session Page',
  events: {
    Started: emptyProps(),
    'Sign In Opened': emptyProps(),
    'Sign In Submitted': props<SignInValue>(),
    'Change Password Submitted': props<ChangePasswordValue>(),
    'Sign Out Clicked': emptyProps(),
    'Sign Out Everywhere Clicked': emptyProps(),
    /** The interceptor could not renew the session: the person has to sign in again. */
    'Session Expired': props<{ returnUrl: string }>(),
  },
});

export const SessionApiActions = createActionGroup({
  source: 'Session API',
  events: {
    'Load Succeeded': props<{ user: SessionUser }>(),
    'Load Failed': emptyProps(),
    'Sign In Succeeded': props<{ response: SignInResponse }>(),
    'Sign In Failed': props<{ error: ApiError }>(),
    'Password Changed': emptyProps(),
    'Password Change Failed': props<{ error: ApiError }>(),
    'Signed Out': emptyProps(),
  },
});
