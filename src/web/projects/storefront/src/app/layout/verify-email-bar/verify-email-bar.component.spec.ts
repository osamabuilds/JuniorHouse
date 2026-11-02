import { TestBed } from '@angular/core/testing';
import { Store, provideStore } from '@ngrx/store';
import { CustomerAuthActions } from '../../core/customer-auth';
import { customerAuthFeature } from '../../core/customer-auth/store/customer-auth.reducer';
import { VerifyEmailBarComponent } from './verify-email-bar.component';

function render(emailVerified: boolean | null) {
  TestBed.configureTestingModule({
    imports: [VerifyEmailBarComponent],
    providers: [provideStore({ [customerAuthFeature.name]: customerAuthFeature.reducer })],
  });
  const store = TestBed.inject(Store);
  if (emailVerified !== null) {
    store.dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'ok', session: { firstName: 'Sana', emailVerified, csrfToken: 'c' } } }));
  }
  const fixture = TestBed.createComponent(VerifyEmailBarComponent);
  fixture.detectChanges();
  return { fixture, store, el: fixture.nativeElement as HTMLElement };
}

describe('VerifyEmailBarComponent', () => {
  it('ACT-31 shows the notice and button for an unverified signed-in customer', () => {
    const { el } = render(false);
    expect(el.querySelector('aside')).not.toBeNull();
    expect(el.querySelector('button')?.textContent).toContain('Verify my email');
  });

  it('ACT-31 shows nothing for a verified customer or a visitor', () => {
    expect(render(true).el.querySelector('aside')).toBeNull();
    TestBed.resetTestingModule();
    expect(render(null).el.querySelector('aside')).toBeNull();
  });

  it('ACT-31 clicking the button asks for a fresh link', () => {
    const { el, store } = render(false);
    const spy = vi.spyOn(store, 'dispatch');
    (el.querySelector('button') as HTMLButtonElement).click();
    expect(spy).toHaveBeenCalledWith(CustomerAuthActions.resendVerificationSubmitted());
  });
});
