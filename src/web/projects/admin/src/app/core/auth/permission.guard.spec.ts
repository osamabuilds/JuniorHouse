import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Store, provideStore } from '@ngrx/store';
import { firstValueFrom, isObservable, Observable } from 'rxjs';
import { permissionGuard } from './permission.guard';
import { SessionApiActions } from './store/auth.actions';
import { sessionReducer } from './store/auth.reducer';
import { SESSION_FEATURE_KEY } from './store/auth.state';

const user = (permissions: string[]) => ({ userId: 7, userName: 'a', displayName: 'A', scope: 'Full', csrfToken: 'c', permissions }) as const;

describe('permissionGuard (roles AC-7)', () => {
  let store: Store;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideStore({ [SESSION_FEATURE_KEY]: sessionReducer })] });
    store = TestBed.inject(Store);
    router = TestBed.inject(Router);
  });

  async function run(permission: string | string[], url = '/vendors'): Promise<boolean | UrlTree> {
    const guard = permissionGuard(permission);
    const result = TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));
    return isObservable(result) ? firstValueFrom(result as Observable<boolean | UrlTree>) : (result as boolean | UrlTree);
  }

  it('lets a person who holds the permission in', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user(['procurement.read']) }));

    expect(await run('procurement.read')).toBe(true);
  });

  it('accepts any one of several permissions', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user(['qc.read']) }));

    expect(await run(['procurement.read', 'qc.read'])).toBe(true);
  });

  it('sends a person without it to the "no access" page, not to sign-in', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user(['catalog.read']) }));

    expect(router.serializeUrl((await run('procurement.read')) as UrlTree)).toBe('/forbidden');
  });

  it('sends an anonymous visitor to sign-in with the page they wanted', async () => {
    store.dispatch(SessionApiActions.loadFailed());

    expect(router.serializeUrl((await run('procurement.read', '/vendors?tab=1')) as UrlTree)).toBe('/sign-in?returnUrl=%2Fvendors%3Ftab%3D1');
  });

  it('waits for the first answer about who the person is', async () => {
    const pending = run('procurement.read');
    let settled = false;
    void pending.then(() => (settled = true));
    await Promise.resolve();
    expect(settled).toBe(false);

    store.dispatch(SessionApiActions.loadSucceeded({ user: user(['procurement.read']) }));

    expect(await pending).toBe(true);
  });

  it('lets anyone signed in reach the "no access" page itself, so it can never loop', async () => {
    store.dispatch(SessionApiActions.loadSucceeded({ user: user([]) }));

    expect(await run('procurement.read', '/forbidden')).toBe(true);
  });
});
