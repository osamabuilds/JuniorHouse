import { inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Store, provideStore } from '@ngrx/store';
import { binRow } from '@app/testing/warehouse-fixtures';
import { BinTableComponent } from '../../features/warehouse/bins/components/bin-table/bin-table.component';
import { PermissionAccess, StorePermissionAccess } from './has-permission.directive';
import { SessionApiActions } from './store/auth.actions';
import { sessionReducer } from './store/auth.reducer';
import { SESSION_FEATURE_KEY } from './store/auth.state';

const user = (permissions: string[]) => ({ userId: 7, userName: 'a', displayName: 'A', scope: 'Full', csrfToken: 'c', permissions }) as const;

/**
 * roles AC-7: write buttons are hidden for a person who may only read, on every screen that has them. This checks one representative
 * screen (the bin list) with the real, session-based check; the same directive guards every other write button, and the server still
 * refuses a direct call (SEC-08).
 */
describe('write buttons are hidden for a read-only person (roles AC-7)', () => {
  function render(permissions: string[]) {
    TestBed.configureTestingModule({
      providers: [provideStore({ [SESSION_FEATURE_KEY]: sessionReducer }), { provide: PermissionAccess, useFactory: () => new StorePermissionAccess(inject(Store)) }],
    });
    TestBed.inject(Store).dispatch(SessionApiActions.loadSucceeded({ user: user(permissions) }));
    const fixture = TestBed.createComponent(BinTableComponent);
    fixture.componentRef.setInput('bins', [binRow()]);
    fixture.componentRef.setInput('loading', false);
    fixture.componentRef.setInput('togglingId', null);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the action to someone who holds the write permission', () => {
    expect(render(['warehouse.read', 'warehouse.write']).querySelectorAll('tbody button').length).toBeGreaterThan(0);
  });

  it('hides it from someone who only reads, but still shows the list', () => {
    const root = render(['warehouse.read']);

    expect(root.querySelectorAll('tbody tr').length).toBe(1);
    expect(root.querySelectorAll('tbody button').length).toBe(0);
  });
});
