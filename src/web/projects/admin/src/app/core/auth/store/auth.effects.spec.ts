import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import type { SessionScope } from '../auth.model';
import { SessionPageActions as Ui } from './auth.actions';
import * as effects from './auth.effects';
import { sessionReducer } from './auth.reducer';
import { selectCsrfToken, selectSessionError, selectSessionStatus, selectSessionUser, selectSigningIn } from './auth.selectors';
import { SESSION_FEATURE_KEY } from './auth.state';

const me = (scope: SessionScope = 'Full') => ({ userId: 7, userName: 'amna.khan', displayName: 'Amna Khan', scope, csrfToken: 'csrf-value' });

describe('session store and effects (sessions AC-1, AC-2, AC-9, AC-11, AC-13)', () => {
  let store: Store;
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore({ [SESSION_FEATURE_KEY]: sessionReducer }),
        provideEffects(effects),
        { provide: Router, useValue: { navigate, navigateByUrl: navigate, url: '/sign-in' } },
      ],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const path = (suffix: string) => (r: { url: string }) => r.url.endsWith(`/api/auth/${suffix}`);

  it('starting asks /me: a caller with a session is signed in, one without is anonymous', () => {
    expect(store.selectSignal(selectSessionStatus)()).toBe('unknown');
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush(me());

    expect(store.selectSignal(selectSessionStatus)()).toBe('authenticated');
    expect(store.selectSignal(selectSessionUser)()?.displayName).toBe('Amna Khan');
    expect(store.selectSignal(selectCsrfToken)()).toBe('csrf-value');
  });

  it('a 401 from /me means anonymous, not an error to show', () => {
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(store.selectSignal(selectSessionStatus)()).toBe('anonymous');
    expect(store.selectSignal(selectSessionError)()).toBeNull();
  });

  it('signing in posts the name and password, then loads who the person is', () => {
    store.dispatch(Ui.signInSubmitted({ userName: 'amna.khan', password: 'a-long-passphrase-1' }));
    expect(store.selectSignal(selectSigningIn)()).toBe(true);

    const signIn = http.expectOne(path('sign-in'));
    expect(signIn.request.body).toEqual({ userName: 'amna.khan', password: 'a-long-passphrase-1' });
    signIn.flush({ status: 'limited', scope: 'ChangePassword' });
    http.expectOne(path('me')).flush(me('ChangePassword'));

    expect(store.selectSignal(selectSigningIn)()).toBe(false);
    expect(store.selectSignal(selectSessionStatus)()).toBe('authenticated');
    expect(store.selectSignal(selectSessionUser)()?.scope).toBe('ChangePassword');
  });

  it('a failed sign-in shows one generic message and never says which part was wrong', () => {
    store.dispatch(Ui.signInSubmitted({ userName: 'nobody', password: 'wrong-one' }));
    http.expectOne(path('sign-in')).flush({ title: 'Sign-in failed' }, { status: 401, statusText: 'Unauthorized' });

    const error = store.selectSignal(selectSessionError)();
    expect(error?.status).toBe(401);
    expect(store.selectSignal(selectSigningIn)()).toBe(false);
    expect(store.selectSignal(selectSessionStatus)()).toBe('unknown');
    expect(error?.message).toBe('Sign-in failed');
  });

  it('a throttled sign-in keeps the number of seconds to wait from Retry-After', () => {
    store.dispatch(Ui.signInSubmitted({ userName: 'amna.khan', password: 'wrong-one' }));
    http.expectOne(path('sign-in')).flush({ title: 'Too many attempts' }, { status: 429, statusText: 'Too Many Requests', headers: { 'Retry-After': '30' } });

    expect(store.selectSignal(selectSessionError)()?.status).toBe(429);
    expect(store.selectSignal(selectSessionError)()?.retryAfterSeconds).toBe(30);
  });

  it('a password that needs a second step is reported, without a session', () => {
    store.dispatch(Ui.signInSubmitted({ userName: 'amna.khan', password: 'a-long-passphrase-1' }));
    http.expectOne(path('sign-in')).flush({ status: 'mfa_required' });

    expect(store.selectSignal(selectSessionStatus)()).toBe('unknown');
    expect(store.selectSignal(selectSessionUser)()).toBeNull();
  });

  it('signing out ends the session on the server, forgets the person and goes to the sign-in page', () => {
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush(me());

    store.dispatch(Ui.signOutClicked());
    http.expectOne((r) => path('sign-out')(r) && r.method === 'POST').flush(null, { status: 204, statusText: 'No Content' });

    expect(store.selectSignal(selectSessionStatus)()).toBe('anonymous');
    expect(store.selectSignal(selectSessionUser)()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/sign-in']);
  });

  it('signing out everywhere calls its own endpoint', () => {
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush(me());

    store.dispatch(Ui.signOutEverywhereClicked());
    http.expectOne(path('sign-out-all')).flush(null, { status: 204, statusText: 'No Content' });

    expect(store.selectSignal(selectSessionStatus)()).toBe('anonymous');
  });

  it('even if the sign-out call fails, this browser forgets the session', () => {
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush(me());

    store.dispatch(Ui.signOutClicked());
    http.expectOne(path('sign-out')).flush({}, { status: 500, statusText: 'Server Error' });

    expect(store.selectSignal(selectSessionStatus)()).toBe('anonymous');
  });

  it('changing the password posts both values, then reloads the session so the scope moves on', () => {
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush(me('ChangePassword'));

    store.dispatch(Ui.changePasswordSubmitted({ currentPassword: 'temporary-one', newPassword: 'a-new-long-passphrase-2' }));
    const change = http.expectOne(path('password'));
    expect(change.request.body).toEqual({ currentPassword: 'temporary-one', newPassword: 'a-new-long-passphrase-2' });
    change.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(path('me')).flush(me('EnrolMfa'));

    expect(store.selectSignal(selectSessionUser)()?.scope).toBe('EnrolMfa');
  });

  it('a rejected password change shows the reason and keeps the session', () => {
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush(me('ChangePassword'));

    store.dispatch(Ui.changePasswordSubmitted({ currentPassword: 'wrong', newPassword: 'short' }));
    http.expectOne(path('password')).flush({ title: 'Bad', detail: 'The new password is too short.' }, { status: 400, statusText: 'Bad Request' });

    expect(store.selectSignal(selectSessionError)()?.message).toBe('The new password is too short.');
    expect(store.selectSignal(selectSessionStatus)()).toBe('authenticated');
  });

  it('an expired session (the interceptor gave up refreshing) sends the person to sign in again, remembering where they were', () => {
    store.dispatch(Ui.started());
    http.expectOne(path('me')).flush(me());

    store.dispatch(Ui.sessionExpired({ returnUrl: '/styles' }));

    expect(store.selectSignal(selectSessionStatus)()).toBe('anonymous');
    expect(navigate).toHaveBeenCalledWith(['/sign-in'], { queryParams: { returnUrl: '/styles' } });
  });

  it('opening the sign-in page clears an earlier error', () => {
    store.dispatch(Ui.signInSubmitted({ userName: 'x', password: 'y' }));
    http.expectOne(path('sign-in')).flush({ title: 'Sign-in failed' }, { status: 401, statusText: 'Unauthorized' });

    store.dispatch(Ui.signInOpened());

    expect(store.selectSignal(selectSessionError)()).toBeNull();
  });
});
