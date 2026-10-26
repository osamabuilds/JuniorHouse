/** What a session may do (sessions plan): everything, or only the one step the account still owes. Mirrors the API's SessionScope. */
export type SessionScope = 'Full' | 'EnrolMfa' | 'ChangePassword';

/** GET /api/auth/me: who the caller is and the anti-forgery value to send on unsafe requests. Held in memory only (AC-13). */
export interface SessionUser {
  readonly userId: number;
  readonly userName: string;
  readonly displayName: string;
  readonly scope: SessionScope;
  readonly csrfToken: string;
}

/** The answer to a password that was accepted: a second step is owed, or the account first has to fix something. */
export interface SignInResponse {
  readonly status: 'mfa_required' | 'limited';
  readonly scope?: SessionScope;
}

export interface SignInValue {
  readonly userName: string;
  readonly password: string;
}

export interface ChangePasswordValue {
  readonly currentPassword: string;
  readonly newPassword: string;
}
