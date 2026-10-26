import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Store, provideStore } from '@ngrx/store';
import { HasPermissionDirective } from './has-permission.directive';
import { SessionApiActions } from './store/auth.actions';
import { sessionReducer } from './store/auth.reducer';
import { SESSION_FEATURE_KEY } from './store/auth.state';

@Component({
  imports: [HasPermissionDirective],
  template: `
    <button type="button" id="write" *hasPermission="'catalog.write'">Edit</button>
    <a id="either" href="/x" *hasPermission="['procurement.read', 'qc.read']">Purchasing or QC</a>
    <p id="nothing" *hasPermission="[]">Anyone who is signed in</p>
  `,
})
class HostComponent {}

const user = (permissions: string[]) => ({ userId: 7, userName: 'a', displayName: 'A', scope: 'Full', csrfToken: 'c', permissions }) as const;

describe('*hasPermission (roles AC-7: hiding is a convenience, the server still checks)', () => {
  function create(permissions: string[] | null) {
    TestBed.configureTestingModule({ providers: [provideStore({ [SESSION_FEATURE_KEY]: sessionReducer })] });
    const store = TestBed.inject(Store);
    if (permissions) {
      store.dispatch(SessionApiActions.loadSucceeded({ user: user(permissions) }));
    }
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, store };
  }

  it('shows what the person holds the permission for and hides the rest', () => {
    const { root } = create(['catalog.read', 'catalog.write']);

    expect(root.querySelector('#write')).not.toBeNull();
    expect(root.querySelector('#either')).toBeNull();
  });

  it('shows an element when any one of several permissions is held', () => {
    const { root } = create(['qc.read']);

    expect(root.querySelector('#either')).not.toBeNull();
    expect(root.querySelector('#write')).toBeNull();
  });

  it('shows everything to a person who holds nothing only where no permission is asked for', () => {
    const { root } = create([]);

    expect(root.querySelector('#write')).toBeNull();
    expect(root.querySelector('#either')).toBeNull();
    expect(root.querySelector('#nothing')).not.toBeNull();
  });

  it('shows nothing before anyone is known, or after sign-out', () => {
    const { root } = create(null);

    expect(root.querySelector('#write')).toBeNull();
    expect(root.querySelector('#nothing')).toBeNull();
  });

  it('follows a change of permissions without a reload', () => {
    const { fixture, root, store } = create(['catalog.write']);
    expect(root.querySelector('#write')).not.toBeNull();

    store.dispatch(SessionApiActions.loadSucceeded({ user: user(['ref.read']) }));
    fixture.detectChanges();

    expect(root.querySelector('#write')).toBeNull();
  });
});
