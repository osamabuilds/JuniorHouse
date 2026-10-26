import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { SESSION_FEATURE_KEY, selectSessionStatus, sessionReducer } from '@core/auth';
import * as sessionEffects from '../../../../core/auth/store/auth.effects';
import { MfaPageActions as Ui } from './mfa.actions';
import * as effects from './mfa.effects';
import { mfaReducer } from './mfa.reducer';
import { selectMfaCodes, selectMfaEnrolment, selectMfaError, selectMfaSaving, selectMfaStatus, selectMfaStep } from './mfa.selectors';
import { MFA_FEATURE_KEY } from './mfa.state';

const ENROLMENT = { otpAuthUri: 'otpauth://totp/Romp:a?secret=ABC', secretText: 'ABCD EFGH', expiresAt: '2026-10-26T09:15:00Z' };
const CODES = ['ABCDE-FGHJK', 'LMNPQ-RSTUV'];

describe('mfa store and effects (mfa AC-1, AC-2, AC-6, AC-7)', () => {
  let store: Store;
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore({ [MFA_FEATURE_KEY]: mfaReducer, [SESSION_FEATURE_KEY]: sessionReducer }),
        provideEffects(effects, sessionEffects),
        { provide: Router, useValue: { navigate, navigateByUrl: navigate, url: '/setup-required' } },
      ],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const path = (suffix: string) => (r: { url: string }) => r.url.endsWith(`/api/auth/${suffix}`);

  function scan() {
    store.dispatch(Ui.opened());
    http.expectOne((r) => path('mfa/enrol/start')(r) && r.method === 'POST').flush(ENROLMENT);
  }

  it('opening the page starts enrolment and holds the secret in memory for scanning', () => {
    store.dispatch(Ui.opened());
    expect(store.selectSignal(selectMfaStep)()).toBe('starting');

    http.expectOne(path('mfa/enrol/start')).flush(ENROLMENT);

    expect(store.selectSignal(selectMfaStep)()).toBe('scanning');
    expect(store.selectSignal(selectMfaEnrolment)()).toEqual(ENROLMENT);
  });

  it('a failure to start shows the reason, with nothing to scan', () => {
    store.dispatch(Ui.opened());
    http.expectOne(path('mfa/enrol/start')).flush({ detail: 'Choose your own password before setting up two-step verification.' }, { status: 409, statusText: 'Conflict' });

    expect(store.selectSignal(selectMfaError)()?.message).toContain('Choose your own password');
    expect(store.selectSignal(selectMfaEnrolment)()).toBeNull();
  });

  it('confirming sends the code, then shows the recovery codes once and reloads who the person is', () => {
    scan();

    store.dispatch(Ui.codeSubmitted({ code: '123456' }));
    expect(store.selectSignal(selectMfaSaving)()).toBe(true);
    const confirm = http.expectOne(path('mfa/enrol/confirm'));
    expect(confirm.request.body).toEqual({ code: '123456' });
    confirm.flush({ recoveryCodes: CODES });
    http.expectOne(path('me')).flush({ userId: 7, userName: 'a', displayName: 'A', scope: 'Full', csrfToken: 'c' });

    expect(store.selectSignal(selectMfaStep)()).toBe('codes');
    expect(store.selectSignal(selectMfaCodes)()).toEqual(CODES);
    expect(store.selectSignal(selectMfaEnrolment)()).toBeNull(); // the secret is dropped once it has served
    expect(store.selectSignal(selectMfaSaving)()).toBe(false);
    expect(store.selectSignal(selectSessionStatus)()).toBe('authenticated');
  });

  it('a wrong code keeps the person on the scanning step with the reason', () => {
    scan();

    store.dispatch(Ui.codeSubmitted({ code: '000000' }));
    http.expectOne(path('mfa/enrol/confirm')).flush({ detail: 'That code is not valid. Check the code in your app and try again.' }, { status: 409, statusText: 'Conflict' });

    expect(store.selectSignal(selectMfaStep)()).toBe('scanning');
    expect(store.selectSignal(selectMfaError)()?.message).toContain('not valid');
    expect(store.selectSignal(selectMfaEnrolment)()).toEqual(ENROLMENT);
    expect(store.selectSignal(selectMfaSaving)()).toBe(false);
  });

  it('saying the codes are saved forgets them and moves on', () => {
    scan();
    store.dispatch(Ui.codeSubmitted({ code: '123456' }));
    http.expectOne(path('mfa/enrol/confirm')).flush({ recoveryCodes: CODES });
    http.expectOne(path('me')).flush({ userId: 7, userName: 'a', displayName: 'A', scope: 'Full', csrfToken: 'c' });

    store.dispatch(Ui.codesSaved());

    expect(store.selectSignal(selectMfaCodes)()).toBeNull();
    expect(store.selectSignal(selectMfaStep)()).toBe('idle');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('starting again replaces the secret', () => {
    scan();

    store.dispatch(Ui.restartClicked());
    http.expectOne(path('mfa/enrol/start')).flush({ ...ENROLMENT, secretText: 'WXYZ 2345' });

    expect(store.selectSignal(selectMfaEnrolment)()?.secretText).toBe('WXYZ 2345');
  });

  it('opening the security page loads the status', () => {
    store.dispatch(Ui.statusOpened());
    http.expectOne((r) => path('mfa')(r) && r.method === 'GET').flush({ enrolled: true, recoveryCodesRemaining: 8 });

    expect(store.selectSignal(selectMfaStatus)()).toEqual({ enrolled: true, recoveryCodesRemaining: 8 });
  });

  it('making new recovery codes sends the current code, shows the codes once and refreshes the count', () => {
    store.dispatch(Ui.statusOpened());
    http.expectOne((r) => path('mfa')(r) && r.method === 'GET').flush({ enrolled: true, recoveryCodesRemaining: 2 });

    store.dispatch(Ui.regenerateSubmitted({ code: '654321' }));
    const request = http.expectOne(path('mfa/recovery-codes'));
    expect(request.request.body).toEqual({ code: '654321' });
    request.flush({ recoveryCodes: CODES });
    http.expectOne((r) => path('mfa')(r) && r.method === 'GET').flush({ enrolled: true, recoveryCodesRemaining: 10 });

    expect(store.selectSignal(selectMfaStep)()).toBe('codes');
    expect(store.selectSignal(selectMfaCodes)()).toEqual(CODES);
    expect(store.selectSignal(selectMfaStatus)()?.recoveryCodesRemaining).toBe(10);
  });

  it('a rejected regeneration shows the reason and keeps the old status', () => {
    store.dispatch(Ui.statusOpened());
    http.expectOne((r) => path('mfa')(r) && r.method === 'GET').flush({ enrolled: true, recoveryCodesRemaining: 2 });

    store.dispatch(Ui.regenerateSubmitted({ code: '000000' }));
    http.expectOne(path('mfa/recovery-codes')).flush({ detail: 'That code is not valid. Wait for the next code in your app and try again.' }, { status: 409, statusText: 'Conflict' });

    expect(store.selectSignal(selectMfaError)()?.message).toContain('not valid');
    expect(store.selectSignal(selectMfaStatus)()?.recoveryCodesRemaining).toBe(2);
    expect(store.selectSignal(selectMfaStep)()).toBe('idle');
  });

  it('writes nothing to browser storage, and the secret and codes are not in the state once dismissed (AC-7)', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    scan();
    store.dispatch(Ui.codeSubmitted({ code: '123456' }));
    http.expectOne(path('mfa/enrol/confirm')).flush({ recoveryCodes: CODES });
    http.expectOne(path('me')).flush({ userId: 7, userName: 'a', displayName: 'A', scope: 'Full', csrfToken: 'c' });
    store.dispatch(Ui.codesSaved());

    let snapshot = '';
    store.subscribe((state) => (snapshot = JSON.stringify(state)));
    expect(setItem).not.toHaveBeenCalled();
    expect(snapshot).not.toContain('ABCDE-FGHJK');
    expect(snapshot).not.toContain('ABCD EFGH');
    setItem.mockRestore();
  });
});
