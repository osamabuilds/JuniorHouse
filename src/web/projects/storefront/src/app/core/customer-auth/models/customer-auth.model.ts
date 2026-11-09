/** What the API answers after a sign-in and on `GET session`: a greeting, the notice-bar flag (ACT-31) and the anti-forgery value. */
export interface AuthSession {
  readonly firstName: string;
  readonly emailVerified: boolean;
  readonly csrfToken: string;
}

export interface RegisterInput {
  readonly firstName: string;
  readonly lastName: string;
  readonly email: string;
  readonly mobile: string;
  readonly password: string;
  readonly keepSignedIn: boolean;
  /** The unticked-by-default box on the form (newsletter NLS-4): only a ticked box subscribes. */
  readonly subscribeNewsletter: boolean;
}

export interface SignInInput {
  readonly email: string;
  readonly password: string;
  readonly keepSignedIn: boolean;
  readonly captchaToken: string | null;
}

/** Every outcome the pages need; nothing throws into the store. */
export type SessionResult = { readonly status: 'ok'; readonly session: AuthSession } | { readonly status: 'signedOut' } | { readonly status: 'error' };

export type SignInResult =
  | { readonly status: 'ok'; readonly session: AuthSession }
  /** One answer for a wrong email, a wrong password and anything else (ACT-5). */
  | { readonly status: 'failed'; readonly captchaRequired: boolean }
  | { readonly status: 'throttled'; readonly retryAfterSeconds: number }
  | { readonly status: 'error' };

export type RegisterResult =
  | { readonly status: 'ok'; readonly session: AuthSession }
  | { readonly status: 'passwordRejected'; readonly unmetRules: readonly string[] }
  | { readonly status: 'emailTaken' }
  | { readonly status: 'invalid'; readonly errors: Readonly<Record<string, string>> }
  | { readonly status: 'error' };

export type SimpleResult = { readonly status: 'ok' } | { readonly status: 'invalid' } | { readonly status: 'tooMany' } | { readonly status: 'error' };

export type ResetResult =
  | { readonly status: 'ok' }
  | { readonly status: 'passwordRejected'; readonly unmetRules: readonly string[] }
  | { readonly status: 'linkInvalid' }
  | { readonly status: 'error' };

/** The run-time settings from `GET /customer-api/config`. */
export interface AuthConfig {
  readonly turnstileSiteKey: string;
}

/** The token the API's development verifier accepts (the real verifier rejects it). Sent only when no Turnstile site key is configured. */
export const DEV_CAPTCHA_TOKEN = 'dev-captcha-pass';

export const AUTH_BASE = '/customer-api/auth';
export const CSRF_HEADER = 'X-Romp-Csrf';
