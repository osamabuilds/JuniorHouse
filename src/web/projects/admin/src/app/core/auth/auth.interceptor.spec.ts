import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Store, provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { authInterceptor } from './auth.interceptor';
import * as sessionEffects from './store/auth.effects';
import { SessionApiActions } from './store/auth.actions';
import { sessionReducer } from './store/auth.reducer';
import { SESSION_FEATURE_KEY } from './store/auth.state';

const signedIn = { userId: 7, userName: 'amna.khan', displayName: 'Amna Khan', scope: 'Full', csrfToken: 'csrf-value', permissions: [] as string[] } as const;

describe('authInterceptor (sessions AC-2, AC-10)', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let store: Store;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideStore({ [SESSION_FEATURE_KEY]: sessionReducer }),
        provideEffects(sessionEffects),
        { provide: Router, useValue: { navigate, navigateByUrl: navigate, url: '/styles' } },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    store = TestBed.inject(Store);
    store.dispatch(SessionApiActions.loadSucceeded({ user: { ...signedIn } }));
  });

  afterEach(() => controller.verify());

  const refreshRequests = () => controller.match((r) => r.url.endsWith('/api/auth/refresh'));

  it('sends the anti-forgery header on unsafe requests only', () => {
    http.post('/api/styles', {}).subscribe();
    http.put('/api/styles/1', {}).subscribe();
    http.patch('/api/styles/1', {}).subscribe();
    http.delete('/api/styles/1').subscribe();
    http.get('/api/styles').subscribe();

    const calls = controller.match(() => true);
    for (const call of calls) {
      const unsafe = call.request.method !== 'GET';
      expect(call.request.headers.get('X-Romp-Csrf')).toBe(unsafe ? 'csrf-value' : null);
      call.flush({});
    }
    expect(calls).toHaveLength(5);
  });

  it('does not send the header for sign-in, which has no session yet', () => {
    store.dispatch(SessionApiActions.loadFailed());

    http.post('/api/auth/sign-in', {}).subscribe();

    const request = controller.expectOne('/api/auth/sign-in');
    expect(request.request.headers.has('X-Romp-Csrf')).toBe(false);
    request.flush({});
  });

  it('on a 401 it refreshes once, then repeats the request', () => {
    let result: unknown;
    http.get('/api/styles').subscribe((value) => (result = value));

    controller.expectOne('/api/styles').flush({}, { status: 401, statusText: 'Unauthorized' });
    refreshRequests()[0].flush(null, { status: 204, statusText: 'No Content' });
    controller.expectOne('/api/styles').flush({ ok: true });

    expect(result).toEqual({ ok: true });
  });

  it('repeats an unsafe request with the header after the refresh', () => {
    http.post('/api/styles', { name: 'x' }).subscribe();

    controller.expectOne('/api/styles').flush({}, { status: 401, statusText: 'Unauthorized' });
    refreshRequests()[0].flush(null, { status: 204, statusText: 'No Content' });
    const again = controller.expectOne('/api/styles');

    expect(again.request.headers.get('X-Romp-Csrf')).toBe('csrf-value');
    expect(again.request.body).toEqual({ name: 'x' });
    again.flush({});
  });

  it('several requests that fail together share one refresh', () => {
    http.get('/api/styles').subscribe();
    http.get('/api/vendors').subscribe();
    http.get('/api/products').subscribe();

    for (const url of ['/api/styles', '/api/vendors', '/api/products']) {
      controller.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
    }
    const refreshes = refreshRequests();
    expect(refreshes).toHaveLength(1);
    refreshes[0].flush(null, { status: 204, statusText: 'No Content' });
    for (const url of ['/api/styles', '/api/vendors', '/api/products']) {
      controller.expectOne(url).flush({});
    }
  });

  it('gives up when the refresh fails: the person is sent to sign in, and the original request fails', () => {
    let error: unknown;
    http.get('/api/styles').subscribe({ error: (e) => (error = e) });

    controller.expectOne('/api/styles').flush({}, { status: 401, statusText: 'Unauthorized' });
    refreshRequests()[0].flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(error).toBeTruthy();
    expect(navigate).toHaveBeenCalledWith(['/sign-in'], { queryParams: { returnUrl: '/styles' } });
  });

  it('never loops: a 401 on the repeated request is returned as it is', () => {
    let error: { status?: number } = {};
    http.get('/api/styles').subscribe({ error: (e) => (error = e) });

    controller.expectOne('/api/styles').flush({}, { status: 401, statusText: 'Unauthorized' });
    refreshRequests()[0].flush(null, { status: 204, statusText: 'No Content' });
    controller.expectOne('/api/styles').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(error.status).toBe(401);
    expect(refreshRequests()).toHaveLength(0);
  });

  it('a 409 from refresh means another request already rotated the cookies: repeat without signing out', () => {
    let result: unknown;
    http.get('/api/styles').subscribe((value) => (result = value));

    controller.expectOne('/api/styles').flush({}, { status: 401, statusText: 'Unauthorized' });
    refreshRequests()[0].flush({}, { status: 409, statusText: 'Conflict' });
    controller.expectOne('/api/styles').flush({ ok: true });

    expect(result).toEqual({ ok: true });
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not try to refresh for the auth endpoints themselves', () => {
    for (const url of ['/api/auth/me', '/api/auth/sign-in', '/api/auth/mfa/verify', '/api/auth/refresh']) {
      let status = 0;
      http.get(url).subscribe({ error: (e) => (status = e.status) });
      controller.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
      expect(status).toBe(401);
    }
    expect(refreshRequests()).toHaveLength(0);
  });

  it('leaves other errors and other hosts alone', () => {
    let status = 0;
    http.get('/api/styles').subscribe({ error: (e) => (status = e.status) });
    controller.expectOne('/api/styles').flush({}, { status: 500, statusText: 'Server Error' });
    expect(status).toBe(500);

    http.post('https://other.example/thing', {}).subscribe();
    const foreign = controller.expectOne('https://other.example/thing');
    expect(foreign.request.headers.has('X-Romp-Csrf')).toBe(false); // the token is never sent to another site
    foreign.flush({});
  });
});
