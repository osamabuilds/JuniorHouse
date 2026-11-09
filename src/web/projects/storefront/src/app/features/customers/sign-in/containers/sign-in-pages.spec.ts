import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CustomerAuthActions, customerAuthFeature } from '../../../../core/customer-auth';
import { CustomerAuthApi } from '../../../../core/customer-auth/services/customer-auth-api.service';
import { SessionRefresher } from '../../../../core/customer-auth/services/session-refresher.service';
import { CustomerAuthEffects } from '../../../../core/customer-auth/store/customer-auth.effects';
import { VerifyEmailBarComponent } from '../../../../layout/verify-email-bar/verify-email-bar.component';
import { provideSiteSettings } from '../../../storefront/site-settings';
import { ForgotPasswordPageContainer } from './forgot-password-page/forgot-password-page.container';
import { RegisterPageContainer } from './register-page/register-page.container';
import { SignInPageContainer } from './sign-in-page/sign-in-page.container';

function setUp(api: Record<string, unknown> = {}, responseInit?: ResponseInit) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({ [customerAuthFeature.name]: customerAuthFeature.reducer }),
      provideEffects(CustomerAuthEffects),
      provideSiteSettings(),
      { provide: SessionRefresher, useValue: { refresh: () => of(true) } },
      { provide: CustomerAuthApi, useValue: { config: () => of({ turnstileSiteKey: '' }), ...api } },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
    ],
  });
  return TestBed.inject(Store);
}

function type(el: HTMLElement, selector: string, value: string): void {
  const input = el.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
  input.dispatchEvent(new Event('blur'));
}

describe('SignInPageContainer', () => {
  function render(api: Record<string, unknown> = {}, responseInit?: ResponseInit) {
    const store = setUp(api, responseInit);
    const fixture = TestBed.createComponent(SignInPageContainer);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    return { store, fixture, el };
  }

  it('ACT-16 has one h1, labels every control, sets autocomplete, and is noindex and never cached', () => {
    const responseInit: ResponseInit = {};
    const { el, fixture } = render({}, responseInit);
    fixture.detectChanges();

    expect(el.querySelectorAll('h1').length).toBe(1);
    el.querySelectorAll('input').forEach((input) => expect(el.querySelector(`label[for="${input.id}"]`) || input.closest('label')).not.toBeNull());
    expect(el.querySelector('input[name="email"]')!.getAttribute('autocomplete')).toBe('email');
    expect(el.querySelector('input[name="password"]')!.getAttribute('autocomplete')).toBe('current-password');
    expect(TestBed.inject(Meta).getTag('name="robots"')!.content).toBe('noindex, nofollow');
    expect(new Headers(responseInit.headers).get('Cache-Control')).toBe('private, no-store');
  });

  it('ACT-5 sends the typed values and, when it fails, says "We could not sign you in" without naming the email or the password', () => {
    const signIn = vi.fn(() => of({ status: 'failed' as const, captchaRequired: false }));
    const { el, fixture } = render({ signIn });

    type(el, 'input[name="email"]', 'sana@example.pk');
    type(el, 'input[name="password"]', 'Wrong1pass');
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(signIn).toHaveBeenCalledWith({ email: 'sana@example.pk', password: 'Wrong1pass', keepSignedIn: false, captchaToken: null });
    const notice = el.querySelector('[role="alert"]')!;
    expect(notice.textContent).toContain('We could not sign you in');
    expect(notice.textContent!.toLowerCase()).not.toMatch(/wrong (email|password)|no account|not registered/);
  });

  it('SEC-02 shows the check once the API asks for it and sends its token with the next attempt', () => {
    const signIn = vi.fn()
      .mockReturnValueOnce(of({ status: 'failed' as const, captchaRequired: true }))
      .mockReturnValueOnce(of({ status: 'failed' as const, captchaRequired: true }));
    const { el, fixture } = render({ signIn });
    type(el, 'input[name="email"]', 'sana@example.pk');
    type(el, 'input[name="password"]', 'Wrong1pass');
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(el.querySelector('rp-turnstile')).not.toBeNull();

    // No site key in this run: the development token is passed on.
    fixture.detectChanges();
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    expect(signIn).toHaveBeenLastCalledWith(expect.objectContaining({ captchaToken: 'dev-captcha-pass' }));
  });

  it('shows what to do next when a field is empty instead of sending the form', () => {
    const signIn = vi.fn();
    const { el, fixture } = render({ signIn });
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(signIn).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Enter the email address you registered with.');
    expect(el.textContent).toContain('Enter your password.');
  });
});

describe('RegisterPageContainer', () => {
  function render(api: Record<string, unknown> = {}) {
    setUp(api);
    const fixture = TestBed.createComponent(RegisterPageContainer);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('ACT-2 shows each password rule as met or not met while typing', () => {
    const { el, fixture } = render();
    const met = () => [...el.querySelectorAll('.rp-rules li')].map((li) => li.classList.contains('is-met'));
    expect(met()).toEqual([false, false, false, false, true]);

    type(el, 'input[name="password"]', 'Sunny9day');
    fixture.detectChanges();
    expect(met()).toEqual([true, true, true, true, true]);

    type(el, 'input[name="password"]', 'sunny 9');
    fixture.detectChanges();
    expect(met()).toEqual([false, false, true, true, false]);
    expect(el.querySelector('.rp-rules')!.textContent).toContain('not met');
  });

  it('ACT-1 sends the form once every field is valid and the terms are ticked', () => {
    const register = vi.fn(() => of({ status: 'ok' as const, session: { firstName: 'Sana', emailVerified: false, csrfToken: 'c' } }));
    const { el, fixture } = render({ register });
    type(el, 'input[name="firstName"]', 'Sana');
    type(el, 'input[name="lastName"]', 'Khan');
    type(el, 'input[name="email"]', 'sana@example.pk');
    type(el, 'input[name="mobile"]', '0300 1234567');
    type(el, 'input[name="password"]', 'Sunny9day');
    const terms = el.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    terms.click();
    fixture.detectChanges();
    el.querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(register).toHaveBeenCalledWith({ firstName: 'Sana', lastName: 'Khan', email: 'sana@example.pk', mobile: '0300 1234567', password: 'Sunny9day', keepSignedIn: false, subscribeNewsletter: false });
  });

  it('NLS-4 the newsletter box starts unticked and is sent only when the customer ticks it', () => {
    const register = vi.fn(() => of({ status: 'ok' as const, session: { firstName: 'Sana', emailVerified: false, csrfToken: 'c' } }));
    const { el, fixture } = render({ register });
    const boxes = el.querySelectorAll<HTMLInputElement>('input[type="checkbox"]');
    const news = boxes[boxes.length - 1];
    expect(news.checked).toBe(false);
    expect(news.closest('label')!.textContent).toContain('news and offers');
    type(el, 'input[name="firstName"]', 'Sana');
    type(el, 'input[name="lastName"]', 'Khan');
    type(el, 'input[name="email"]', 'sana@example.pk');
    type(el, 'input[name="mobile"]', '0300 1234567');
    type(el, 'input[name="password"]', 'Sunny9day');
    boxes[0].click();
    news.click();
    fixture.detectChanges();
    el.querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(register).toHaveBeenCalledWith(expect.objectContaining({ subscribeNewsletter: true }));
  });

  it('ACT-3 says the email already has an account and offers sign-in or reset', () => {
    const { el, fixture } = render({ register: () => of({ status: 'emailTaken' as const }) });
    type(el, 'input[name="firstName"]', 'Sana');
    type(el, 'input[name="lastName"]', 'Khan');
    type(el, 'input[name="email"]', 'sana@example.pk');
    type(el, 'input[name="mobile"]', '03001234567');
    type(el, 'input[name="password"]', 'Sunny9day');
    el.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click();
    fixture.detectChanges();
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const notice = el.querySelector('[role="alert"]')!;
    expect(notice.textContent).toContain('already has an account');
    expect(notice.querySelector('a[href="/account/sign-in"]')).not.toBeNull();
    expect(notice.querySelector('a[href="/account/forgot-password"]')).not.toBeNull();
  });

  it('does not send the form without the terms ticked, and says so', () => {
    const register = vi.fn();
    const { el, fixture } = render({ register });
    type(el, 'input[name="firstName"]', 'Sana');
    type(el, 'input[name="lastName"]', 'Khan');
    type(el, 'input[name="email"]', 'sana@example.pk');
    type(el, 'input[name="mobile"]', '03001234567');
    type(el, 'input[name="password"]', 'Sunny9day');
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(register).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Tick the box to agree');
  });
});

describe('ForgotPasswordPageContainer', () => {
  it('ACT-6 shows the same confirmation whatever the address, without saying whether it has an account', () => {
    setUp({ requestReset: () => of({ status: 'ok' as const }) });
    const fixture = TestBed.createComponent(ForgotPasswordPageContainer);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    type(el, 'input[name="email"]', 'nobody@example.pk');
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(el.querySelector('h1')!.textContent).toContain('Check your email');
    expect(el.textContent).toContain('If an account exists for that email address, a reset link is on its way');
  });
});

describe('VerifyEmailBarComponent', () => {
  function render(verified: boolean | null, api: Record<string, unknown> = {}) {
    const store = setUp(api);
    if (verified !== null) store.dispatch(CustomerAuthActions.sessionLoaded({ result: { status: 'ok', session: { firstName: 'Sana', emailVerified: verified, csrfToken: 'csrf-1' } } }));
    const fixture = TestBed.createComponent(VerifyEmailBarComponent);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('ACT-31 shows for a signed-in customer with an unverified email, and not otherwise', () => {
    expect(render(false).el.querySelector('aside')).not.toBeNull();
    TestBed.resetTestingModule();
    expect(render(true).el.querySelector('aside')).toBeNull();
    TestBed.resetTestingModule();
    expect(render(null).el.querySelector('aside')).toBeNull();
  });

  it('ACT-31 the "Verify my email" button asks for a fresh link and the bar then says it is on its way', () => {
    const resendVerification = vi.fn(() => of({ status: 'ok' as const }));
    const { el, fixture } = render(false, { resendVerification });
    el.querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    expect(resendVerification).toHaveBeenCalledWith('csrf-1');
    expect(el.textContent).toContain('A new verification link is on its way');
    expect(el.querySelector('button')).toBeNull();
  });
});
