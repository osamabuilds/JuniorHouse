import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthSession, SessionResult } from '../models/customer-auth.model';
import { CustomerAuthApi } from '../services/customer-auth-api.service';
import { SessionRefresher } from '../services/session-refresher.service';
import { CustomerAuthActions } from './customer-auth.actions';
import { CustomerAuthEffects } from './customer-auth.effects';
import { customerAuthFeature } from './customer-auth.reducer';
import { selectAuthStatus, selectResendStatus, selectResetRequest, selectShowVerifyNotice, selectSignInForm } from './customer-auth.selectors';

const session = (emailVerified: boolean): AuthSession => ({ firstName: 'Sana', emailVerified, csrfToken: 'csrf-1' });

function setUp(api: Partial<Record<keyof CustomerAuthApi, unknown>>, refresh = of(true)) {
  TestBed.configureTestingModule({
    providers: [
      provideStore({ [customerAuthFeature.name]: customerAuthFeature.reducer }),
      provideEffects(CustomerAuthEffects),
      { provide: CustomerAuthApi, useValue: api },
      { provide: SessionRefresher, useValue: { refresh: () => refresh } },
    ],
  });
  return TestBed.inject(Store);
}

describe('customer auth store', () => {
  it('ACT-31 shows the notice only for a signed-in customer whose email is not verified', () => {
    const store = setUp({ session: () => of<SessionResult>({ status: 'ok', session: session(false) }) });
    expect(store.selectSignal(selectShowVerifyNotice)()).toBe(false); // still unknown
    store.dispatch(CustomerAuthActions.sessionRequested());
    expect(store.selectSignal(selectAuthStatus)()).toBe('signedIn');
    expect(store.selectSignal(selectShowVerifyNotice)()).toBe(true);
    store.dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'ok', session: session(true) } }));
    expect(store.selectSignal(selectShowVerifyNotice)()).toBe(false);
  });

  it('ACT-31 sends a fresh link with the anti-forgery value and reports it', () => {
    const resendVerification = vi.fn(() => of({ status: 'ok' as const }));
    const store = setUp({ resendVerification });
    store.dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'ok', session: session(false) } }));
    store.dispatch(CustomerAuthActions.resendVerificationSubmitted());
    expect(resendVerification).toHaveBeenCalledWith('csrf-1');
    expect(store.selectSignal(selectResendStatus)()).toBe('sent');
  });

  it('ACT-4 a successful sign-in makes the customer signed in', () => {
    const store = setUp({ signIn: () => of({ status: 'ok' as const, session: session(true) }) });
    store.dispatch(CustomerAuthActions.signInSubmitted({ input: { email: 'a@b.pk', password: 'x', keepSignedIn: false, captchaToken: null } }));
    expect(store.selectSignal(selectAuthStatus)()).toBe('signedIn');
  });

  it('ACT-5 one failure state covers every kind of wrong answer', () => {
    const store = setUp({ signIn: () => of({ status: 'failed' as const, captchaRequired: false }) });
    store.dispatch(CustomerAuthActions.signInSubmitted({ input: { email: 'a@b.pk', password: 'x', keepSignedIn: false, captchaToken: null } }));
    const form = store.selectSignal(selectSignInForm)();
    expect(form.failed).toBe(true);
    expect(form.captchaRequired).toBe(false);
    expect(store.selectSignal(selectAuthStatus)()).toBe('unknown');
  });

  it('ACT-9 once the API asks for the check it stays asked for the whole visit, and a throttle carries the wait', () => {
    const results = [{ status: 'failed' as const, captchaRequired: true }, { status: 'throttled' as const, retryAfterSeconds: 8 }];
    const store = setUp({ signIn: () => of(results.shift()!) });
    const submit = () => store.dispatch(CustomerAuthActions.signInSubmitted({ input: { email: 'a@b.pk', password: 'x', keepSignedIn: false, captchaToken: null } }));
    submit();
    expect(store.selectSignal(selectSignInForm)().captchaRequired).toBe(true);
    store.dispatch(CustomerAuthActions.formOpened());
    expect(store.selectSignal(selectSignInForm)().captchaRequired).toBe(true);
    submit();
    expect(store.selectSignal(selectSignInForm)().retryAfterSeconds).toBe(8);
  });

  it('ACT-6 the reset page says the same thing for every answer except a failure to ask', () => {
    for (const status of ['ok', 'invalid', 'tooMany'] as const) {
      TestBed.resetTestingModule();
      const store = setUp({ requestReset: () => of({ status }) });
      store.dispatch(CustomerAuthActions.resetRequested({ email: 'x@y.pk' }));
      expect(store.selectSignal(selectResetRequest)()).toEqual({ busy: false, sent: true, error: false });
    }
    TestBed.resetTestingModule();
    const store = setUp({ requestReset: () => of({ status: 'error' as const }) });
    store.dispatch(CustomerAuthActions.resetRequested({ email: 'x@y.pk' }));
    expect(store.selectSignal(selectResetRequest)().error).toBe(true);
  });

  it('ACT-12 an expired access cookie is renewed once in the browser and the session is read again', () => {
    const calls: SessionResult[] = [{ status: 'signedOut' }, { status: 'ok', session: session(true) }];
    const store = setUp({ session: () => of(calls.shift()!) });
    store.dispatch(CustomerAuthActions.sessionRequested());
    expect(store.selectSignal(selectAuthStatus)()).toBe('signedIn');
  });

  it('ACT-12 a refresh that fails leaves the customer signed out', () => {
    const store = setUp({ session: () => of<SessionResult>({ status: 'signedOut' }) }, of(false));
    store.dispatch(CustomerAuthActions.sessionRequested());
    expect(store.selectSignal(selectAuthStatus)()).toBe('signedOut');
  });

  it('ACT-11 signing out clears the session', () => {
    const store = setUp({ signOut: () => of(undefined) });
    store.dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'ok', session: session(true) } }));
    store.dispatch(CustomerAuthActions.signOutSubmitted());
    expect(store.selectSignal(selectAuthStatus)()).toBe('signedOut');
  });

  it('ACT-27 a verification link that works marks the email verified and reads the session again', () => {
    const sessions: SessionResult[] = [{ status: 'ok', session: session(true) }];
    const store = setUp({ verifyEmail: () => of({ status: 'ok' as const }), session: () => of(sessions.shift()!) });
    store.dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'ok', session: session(false) } }));
    store.dispatch(CustomerAuthActions.verifyEmailSubmitted({ token: 't' }));
    expect(store.selectSignal(selectShowVerifyNotice)()).toBe(false);
  });
});
