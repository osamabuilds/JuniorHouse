import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { RolesPageActions } from './roles.actions';
import * as effects from './roles.effects';
import { rolesReducer } from './roles.reducer';
import { selectRoleMatrix, selectRolesError, selectRolesLoading } from './roles.selectors';
import { ROLES_FEATURE_KEY } from './roles.state';

describe('roles store (roles AC-6)', () => {
  let store: Store;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideStore({ [ROLES_FEATURE_KEY]: rolesReducer }), provideEffects(effects)],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('opening the page loads the matrix', () => {
    store.dispatch(RolesPageActions.opened());
    expect(store.selectSignal(selectRolesLoading)()).toBe(true);

    http.expectOne((r) => r.url.endsWith('/api/identity/roles/matrix')).flush({ permissions: [], roles: [] });

    expect(store.selectSignal(selectRoleMatrix)()).toEqual({ permissions: [], roles: [] });
    expect(store.selectSignal(selectRolesLoading)()).toBe(false);
  });

  it('a failure shows the reason and no matrix', () => {
    store.dispatch(RolesPageActions.opened());
    http.expectOne((r) => r.url.endsWith('/api/identity/roles/matrix')).flush({}, { status: 403, statusText: 'Forbidden' });

    expect(store.selectSignal(selectRoleMatrix)()).toBeNull();
    expect(store.selectSignal(selectRolesError)()?.status).toBe(403);
    expect(store.selectSignal(selectRolesLoading)()).toBe(false);
  });
});
