import { createFeature, createReducer, on } from '@ngrx/store';
import { AuthSession } from '../models/customer-auth.model';
import { CustomerAuthActions } from './customer-auth.actions';

/** `unknown` until the first answer from the API, so the header does not flash a sign-in link at a signed-in customer. */
export type AuthStatus = 'unknown' | 'signedIn' | 'signedOut';
export type ResendStatus = 'idle' | 'busy' | 'sent' | 'tooMany' | 'error';
export type VerifyStatus = 'idle' | 'busy' | 'ok' | 'invalid' | 'error';
export type ResetConfirmStatus = 'idle' | 'busy' | 'ok' | 'linkInvalid' | 'error';

export interface SignInFormState {
  readonly busy: boolean;
  /** The one message for every kind of failure (ACT-5). */
  readonly failed: boolean;
  readonly captchaRequired: boolean;
  readonly retryAfterSeconds: number | null;
  readonly error: boolean;
}

export interface RegisterFormState {
  readonly busy: boolean;
  readonly emailTaken: boolean;
  readonly unmetRules: readonly string[];
  readonly fieldErrors: Readonly<Record<string, string>>;
  readonly error: boolean;
}

export interface CustomerAuthState {
  readonly status: AuthStatus;
  readonly session: AuthSession | null;
  readonly signIn: SignInFormState;
  readonly register: RegisterFormState;
  readonly resetRequest: { readonly busy: boolean; readonly sent: boolean; readonly error: boolean };
  readonly resetConfirm: { readonly status: ResetConfirmStatus; readonly unmetRules: readonly string[] };
  readonly verify: VerifyStatus;
  readonly resend: ResendStatus;
}

const cleanSignIn: SignInFormState = { busy: false, failed: false, captchaRequired: false, retryAfterSeconds: null, error: false };
const cleanRegister: RegisterFormState = { busy: false, emailTaken: false, unmetRules: [], fieldErrors: {}, error: false };

export const initialCustomerAuthState: CustomerAuthState = {
  status: 'unknown',
  session: null,
  signIn: cleanSignIn,
  register: cleanRegister,
  resetRequest: { busy: false, sent: false, error: false },
  resetConfirm: { status: 'idle', unmetRules: [] },
  verify: 'idle',
  resend: 'idle',
};

export const customerAuthFeature = createFeature({
  name: 'customerAuth',
  reducer: createReducer(
    initialCustomerAuthState,
    on(CustomerAuthActions.sessionLoaded, (s, { result }): CustomerAuthState => {
      if (result.status === 'ok') return { ...s, status: 'signedIn', session: result.session };
      if (result.status === 'signedOut') return { ...s, status: 'signedOut', session: null };
      return s.status === 'unknown' ? { ...s, status: 'signedOut' } : s;
    }),
    // The captcha requirement belongs to the visit, so it survives opening the form again.
    on(CustomerAuthActions.formOpened, (s): CustomerAuthState => ({
      ...s,
      signIn: { ...cleanSignIn, captchaRequired: s.signIn.captchaRequired },
      register: cleanRegister,
      resetRequest: { busy: false, sent: false, error: false },
      resetConfirm: { status: 'idle', unmetRules: [] },
      verify: 'idle',
    })),
    on(CustomerAuthActions.signInSubmitted, (s): CustomerAuthState => ({ ...s, signIn: { ...cleanSignIn, busy: true, captchaRequired: s.signIn.captchaRequired } })),
    on(CustomerAuthActions.signInDone, (s, { result }): CustomerAuthState => {
      switch (result.status) {
        case 'ok':
          return { ...s, status: 'signedIn', session: result.session, signIn: cleanSignIn };
        case 'failed':
          return { ...s, signIn: { ...cleanSignIn, failed: true, captchaRequired: result.captchaRequired || s.signIn.captchaRequired } };
        case 'throttled':
          return { ...s, signIn: { ...cleanSignIn, failed: true, captchaRequired: true, retryAfterSeconds: result.retryAfterSeconds } };
        default:
          return { ...s, signIn: { ...cleanSignIn, captchaRequired: s.signIn.captchaRequired, error: true } };
      }
    }),
    on(CustomerAuthActions.registerSubmitted, (s): CustomerAuthState => ({ ...s, register: { ...cleanRegister, busy: true } })),
    on(CustomerAuthActions.registerDone, (s, { result }): CustomerAuthState => {
      switch (result.status) {
        case 'ok':
          return { ...s, status: 'signedIn', session: result.session, register: cleanRegister };
        case 'emailTaken':
          return { ...s, register: { ...cleanRegister, emailTaken: true } };
        case 'passwordRejected':
          return { ...s, register: { ...cleanRegister, unmetRules: result.unmetRules } };
        case 'invalid':
          return { ...s, register: { ...cleanRegister, fieldErrors: result.errors } };
        default:
          return { ...s, register: { ...cleanRegister, error: true } };
      }
    }),
    on(CustomerAuthActions.signOutDone, (s): CustomerAuthState => ({ ...initialCustomerAuthState, status: 'signedOut', signIn: { ...cleanSignIn, captchaRequired: s.signIn.captchaRequired } })),
    on(CustomerAuthActions.resetRequested, (s): CustomerAuthState => ({ ...s, resetRequest: { busy: true, sent: false, error: false } })),
    on(CustomerAuthActions.resetRequestDone, (s, { result }): CustomerAuthState => ({
      ...s,
      // The page says the same thing whether or not the address has an account (ACT-6); only a failure to ask is different.
      resetRequest: { busy: false, sent: result.status !== 'error', error: result.status === 'error' },
    })),
    on(CustomerAuthActions.resetConfirmed, (s): CustomerAuthState => ({ ...s, resetConfirm: { status: 'busy', unmetRules: [] } })),
    on(CustomerAuthActions.resetConfirmDone, (s, { result }): CustomerAuthState => ({
      ...s,
      resetConfirm: {
        status: result.status === 'ok' ? 'ok' : result.status === 'linkInvalid' ? 'linkInvalid' : result.status === 'error' ? 'error' : 'idle',
        unmetRules: result.status === 'passwordRejected' ? result.unmetRules : [],
      },
    })),
    on(CustomerAuthActions.verifyEmailSubmitted, (s): CustomerAuthState => ({ ...s, verify: 'busy' })),
    on(CustomerAuthActions.verifyEmailDone, (s, { result }): CustomerAuthState => ({
      ...s,
      verify: result.status === 'ok' ? 'ok' : result.status === 'invalid' ? 'invalid' : 'error',
      session: result.status === 'ok' && s.session ? { ...s.session, emailVerified: true } : s.session,
    })),
    on(CustomerAuthActions.resendVerificationSubmitted, (s): CustomerAuthState => ({ ...s, resend: 'busy' })),
    on(CustomerAuthActions.resendVerificationDone, (s, { result }): CustomerAuthState => ({
      ...s,
      resend: result.status === 'ok' ? 'sent' : result.status === 'tooMany' ? 'tooMany' : 'error',
    })),
  ),
});
