import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { roleOption, staffMember, temporaryPassword } from '@app/testing/identity-fixtures';
import { StaffPageActions as Ui } from './staff.actions';
import * as effects from './staff.effects';
import { staffReducer } from './staff.reducer';
import {
  selectOneTimePassword,
  selectSelectedStaff,
  selectStaff,
  selectStaffError,
  selectStaffMode,
  selectStaffRoles,
  selectStaffSaving,
} from './staff.selectors';
import { STAFF_FEATURE_KEY } from './staff.state';

const emptyPage = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };

describe('staff effects and reducer (spec AC-4, AC-8, AC-9, AC-11)', () => {
  let store: Store;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore({ [STAFF_FEATURE_KEY]: staffReducer }),
        provideEffects(effects),
      ],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const path = (suffix: string) => (r: { url: string }) => r.url.endsWith(`/api/identity/${suffix}`);
  const listRequest = () => http.expectOne((r) => r.url.endsWith('/api/identity/staff') && r.method === 'GET');

  function open() {
    store.dispatch(Ui.opened());
    listRequest().flush({ ...emptyPage, items: [staffMember()], totalCount: 1 });
    http.expectOne(path('roles')).flush([roleOption()]);
  }

  it('opening loads the first page and the role choices', () => {
    open();

    expect(store.selectSignal(selectStaff)()).toHaveLength(1);
    expect(store.selectSignal(selectStaffRoles)()).toHaveLength(1);
  });

  it('searching goes back to page one and sends the text; "active only" sends isActive', () => {
    open();

    store.dispatch(Ui.searchChanged({ searchText: 'amna' }));
    expect(listRequest().request.params.get('search')).toBe('amna');
    store.dispatch(Ui.activeOnlyToggled());
    const request = listRequest();
    expect(request.request.params.get('isActive')).toBe('true');
    expect(request.request.params.get('page')).toBe('1');
    request.flush(emptyPage);
  });

  it('creating holds the temporary password in the store, reloads the list, and dismissing drops it (AC-4)', () => {
    open();
    store.dispatch(Ui.createStarted());

    store.dispatch(Ui.createSubmitted({ value: { userName: 'amna.khan', displayName: 'Amna Khan', roleIds: [2] } }));
    expect(store.selectSignal(selectStaffSaving)()).toBe(true);
    const create = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/api/identity/staff'));
    expect(create.request.body).toEqual({ userName: 'amna.khan', displayName: 'Amna Khan', roleIds: [2] });
    create.flush(temporaryPassword());
    listRequest().flush(emptyPage);

    expect(store.selectSignal(selectOneTimePassword)()?.temporaryPassword).toBe('Xk7m-Qp4t-Rv9w-Zc2n');
    expect(store.selectSignal(selectStaffMode)()).toBe('list');

    store.dispatch(Ui.passwordDismissed());
    expect(store.selectSignal(selectOneTimePassword)()).toBeNull();
  });

  it('opening the screen again also drops a password that was still held', () => {
    open();
    store.dispatch(Ui.createSubmitted({ value: { userName: 'amna.khan', displayName: 'A', roleIds: [] } }));
    http.expectOne((r) => r.method === 'POST').flush(temporaryPassword());
    listRequest().flush(emptyPage);

    store.dispatch(Ui.opened());
    listRequest().flush(emptyPage);
    http.expectOne(path('roles')).flush([]);

    expect(store.selectSignal(selectOneTimePassword)()).toBeNull();
  });

  it('a refused create (for example a taken name) keeps the form and shows the error', () => {
    open();
    store.dispatch(Ui.createSubmitted({ value: { userName: 'amna.khan', displayName: 'A', roleIds: [] } }));

    http.expectOne((r) => r.method === 'POST').flush({ title: 'Conflict', detail: 'That user name is taken.' }, { status: 409, statusText: 'Conflict' });

    expect(store.selectSignal(selectStaffError)()?.status).toBe(409);
    expect(store.selectSignal(selectStaffSaving)()).toBe(false);
    expect(store.selectSignal(selectOneTimePassword)()).toBeNull();
  });

  it('deactivating posts to the account and then shows it as it now is (AC-8)', () => {
    open();
    store.dispatch(Ui.staffSelected({ id: 11 }));
    http.expectOne(path('staff/11')).flush(staffMember());

    store.dispatch(Ui.deactivateClicked({ id: 11 }));
    http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/staff/11/deactivate')).flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(path('staff/11')).flush(staffMember({ isActive: false }));

    expect(store.selectSignal(selectSelectedStaff)()?.isActive).toBe(false);
    expect(store.selectSignal(selectStaffSaving)()).toBe(false);
  });

  it('the last-Owner refusal (409) from a deactivation is shown, not swallowed (AC-9)', () => {
    open();
    store.dispatch(Ui.deactivateClicked({ id: 1 }));

    http.expectOne((r) => r.url.endsWith('/staff/1/deactivate')).flush({ title: 'Conflict', detail: 'The last active Owner cannot be deactivated.' }, { status: 409, statusText: 'Conflict' });

    expect(store.selectSignal(selectStaffError)()?.message).toContain('last active Owner');
  });

  it('resetting makes a new password and shows it once (AC-4)', () => {
    open();

    store.dispatch(Ui.resetPasswordClicked({ id: 11 }));
    http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/staff/11/reset-password')).flush(temporaryPassword({ temporaryPassword: 'New-Pass-1234-abcd' }));

    expect(store.selectSignal(selectOneTimePassword)()?.temporaryPassword).toBe('New-Pass-1234-abcd');
  });
});

describe('changing roles (roles AC-6)', () => {
  let store: Store;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideStore({ [STAFF_FEATURE_KEY]: staffReducer }), provideEffects(effects)],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the whole set, then shows the account as it now is', () => {
    store.dispatch(Ui.staffSelected({ id: 11 }));
    http.expectOne((r) => r.url.endsWith('/api/identity/staff/11') && r.method === 'GET').flush(staffMember());

    store.dispatch(Ui.rolesSubmitted({ id: 11, roleIds: [2, 3] }));
    expect(store.selectSignal(selectStaffSaving)()).toBe(true);
    const put = http.expectOne((r) => r.method === 'PUT' && r.url.endsWith('/api/identity/staff/11/roles'));
    expect(put.request.body).toEqual({ roleIds: [2, 3] });
    put.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne((r) => r.url.endsWith('/api/identity/staff/11') && r.method === 'GET').flush(
      staffMember({ roles: [{ id: 2, code: 'CATALOGUE', name: 'Catalogue' }, { id: 3, code: 'ORDER_OPS', name: 'Order-ops' }] }),
    );

    expect(store.selectSignal(selectSelectedStaff)()?.roles).toHaveLength(2);
    expect(store.selectSignal(selectStaffSaving)()).toBe(false);
  });

  it('the last-Owner refusal is shown and nothing changes', () => {
    store.dispatch(Ui.rolesSubmitted({ id: 1, roleIds: [2] }));

    http.expectOne((r) => r.method === 'PUT' && r.url.endsWith('/staff/1/roles')).flush(
      { title: 'Conflict', detail: 'This is the last active Owner. Make someone else an Owner first.' },
      { status: 409, statusText: 'Conflict' },
    );

    expect(store.selectSignal(selectStaffError)()?.message).toContain('last active Owner');
    expect(store.selectSignal(selectStaffSaving)()).toBe(false);
  });
});
