import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { SessionPageActions as Ui } from './store/auth.actions';
import * as effects from './store/auth.effects';
import { sessionReducer } from './store/auth.reducer';
import { SESSION_FEATURE_KEY } from './store/auth.state';

describe('no credential is kept in the browser (sessions AC-13)', () => {
  const CANARY_PASSWORD = 'canary-passphrase-4471';
  const CANARY_CSRF = 'canary-csrf-value-9921';

  it('writes nothing to local storage, session storage or script-readable cookies through sign-in, password change and sign-out', () => {
    localStorage.clear();
    sessionStorage.clear();
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore({ [SESSION_FEATURE_KEY]: sessionReducer }),
        provideEffects(effects),
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true), navigateByUrl: vi.fn(), url: '/sign-in' } },
      ],
    });
    const store = TestBed.inject(Store);
    const http = TestBed.inject(HttpTestingController);
    const path = (suffix: string) => (r: { url: string }) => r.url.endsWith(`/api/auth/${suffix}`);
    const user = { userId: 1, userName: 'amna.khan', displayName: 'Amna Khan', scope: 'Full', csrfToken: CANARY_CSRF };

    store.dispatch(Ui.signInSubmitted({ userName: 'amna.khan', password: CANARY_PASSWORD }));
    http.expectOne(path('sign-in')).flush({ status: 'limited', scope: 'Full' });
    http.expectOne(path('me')).flush(user);
    store.dispatch(Ui.changePasswordSubmitted({ currentPassword: CANARY_PASSWORD, newPassword: CANARY_PASSWORD + '!' }));
    http.expectOne(path('password')).flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(path('me')).flush(user);
    store.dispatch(Ui.signOutClicked());
    http.expectOne(path('sign-out')).flush(null, { status: 204, statusText: 'No Content' });

    expect(setItem).not.toHaveBeenCalled();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
    expect(document.cookie).not.toContain(CANARY_PASSWORD);
    expect(document.cookie).not.toContain(CANARY_CSRF);
    http.verify();
    setItem.mockRestore();
  });

  it('keeps the password out of the state: it is sent, never stored', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore({ [SESSION_FEATURE_KEY]: sessionReducer }),
        provideEffects(effects),
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true), navigateByUrl: vi.fn(), url: '/sign-in' } },
      ],
    });
    const store = TestBed.inject(Store);
    const http = TestBed.inject(HttpTestingController);

    store.dispatch(Ui.signInSubmitted({ userName: 'amna.khan', password: CANARY_PASSWORD }));
    http.expectOne((r) => r.url.endsWith('/api/auth/sign-in')).flush({ title: 'Sign-in failed' }, { status: 401, statusText: 'Unauthorized' });

    let snapshot = '';
    store.subscribe((state) => (snapshot = JSON.stringify(state)));
    expect(snapshot).not.toContain(CANARY_PASSWORD);
  });
});
