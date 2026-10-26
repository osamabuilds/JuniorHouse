import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Store, provideStore } from '@ngrx/store';
import { firstValueFrom, isObservable, Observable } from 'rxjs';
import { authGuard } from './auth.guard';
import type { SessionScope } from './auth.model';
import { SessionApiActions } from './store/auth.actions';
import { sessionReducer } from './store/auth.reducer';
import { SESSION_FEATURE_KEY } from './store/auth.state';

const user = (scope: SessionScope) => ({ userId: 7, userName: 'amna.khan', displayName: 'Amna Khan', scope, csrfToken: 'c', permissions: [] as string[] }) as const;

describe('authGuard (sessions AC-9)', () => {
  let store: Store;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideStore({ [SESSION_FEATURE_KEY]: sessionReducer })] });
    store = TestBed.inject(Store);
    router = TestBed.inject(Router);
  });

  async function run(url: string): Promise<boolean | UrlTree> {
    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));
    return isObservable(result) ? firstValueFrom(result as Observable<boolean | UrlTree>) : (result as boolean | UrlTree);
  }

  it('lets a fully signed-in person through', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user('Full') }));

    expect(await run('/styles')).toBe(true);
  });

  it('sends an anonymous visitor to sign in, remembering the page', async () => {
    store.dispatch(SessionApiActions.loadFailed());

    const result = await run('/styles?tab=2');

    expect(router.serializeUrl(result as UrlTree)).toBe('/sign-in?returnUrl=%2Fstyles%3Ftab%3D2');
  });

  it('waits until the first /me answer instead of flashing the sign-in page', async () => {
    const pending = run('/styles');
    let settled = false;
    void pending.then(() => (settled = true));
    await Promise.resolve();
    expect(settled).toBe(false);

    store.dispatch(SessionApiActions.loadSucceeded({ user: user('Full') }));

    expect(await pending).toBe(true);
  });

  it('keeps a person who must change their password on that page', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user('ChangePassword') }));

    expect(router.serializeUrl((await run('/styles')) as UrlTree)).toBe('/change-password');
  });

  it('keeps a person who must set up two-step verification on its notice page', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user('EnrolMfa') }));

    expect(router.serializeUrl((await run('/styles')) as UrlTree)).toBe('/setup-required');
  });

  it('lets the limited scopes reach exactly their own page', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user('ChangePassword') }));
    expect(await run('/change-password')).toBe(true);

    store.dispatch(SessionApiActions.loadSucceeded({ user: user('EnrolMfa') }));
    expect(await run('/setup-required')).toBe(true);
    expect(router.serializeUrl((await run('/change-password')) as UrlTree)).toBe('/setup-required');
  });

  it('does not ask a vendor for a staff sign-in on the vendor-facing page', async () => {
    store.dispatch(SessionApiActions.loadFailed());

    expect(await run('/purchase-orders/10/vendor-view')).toBe(true);
  });

  it('sends a fully signed-in person away from the limited pages', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user('Full') }));

    expect(router.serializeUrl((await run('/change-password')) as UrlTree)).toBe('/');
  });
});
