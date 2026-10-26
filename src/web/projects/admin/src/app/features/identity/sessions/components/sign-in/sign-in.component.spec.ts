import { TestBed } from '@angular/core/testing';
import { controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { SignInComponent, SignInValue } from './sign-in.component';

describe('SignInComponent (sessions AC-7, AC-13)', () => {
  function create() {
    const fixture = TestBed.createComponent(SignInComponent);
    const submitted = vi.fn<(value: SignInValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  it('has one heading, labelled user name and password controls, and a submit button', () => {
    const { root } = create();

    expect(root.querySelectorAll('h1')).toHaveLength(1);
    expect(controlsWithoutLabels(root)).toEqual([]);
    expect(root.querySelector('input#si-user')).not.toBeNull();
    expect((root.querySelector('input#si-password') as HTMLInputElement).type).toBe('password');
    expect(root.querySelector('button[type="submit"]')?.textContent).toContain('Sign in');
  });

  it('lets the browser and password managers do their job', () => {
    const { root } = create();

    expect(root.querySelector('#si-user')?.getAttribute('autocomplete')).toBe('username');
    expect(root.querySelector('#si-password')?.getAttribute('autocomplete')).toBe('current-password');
  });

  it('submits what was typed, untrimmed for the password', () => {
    const { root, submitted } = create();
    typeInto(root, '#si-user', 'amna.khan');
    typeInto(root, '#si-password', '  spaced passphrase  ');

    submit(root);

    expect(submitted).toHaveBeenCalledWith({ userName: 'amna.khan', password: '  spaced passphrase  ' });
  });

  it('does not submit an empty form and asks for the missing pieces', () => {
    const { fixture, root, submitted } = create();

    submit(root);
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Enter your user name and password');
  });

  it('shows one generic message for a failed sign-in, whichever part was wrong (AC-7)', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 401, message: 'Sign-in failed', fieldErrors: {} });
    fixture.detectChanges();

    const alert = root.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain("That user name and password didn't work");
    expect(alert?.textContent).not.toMatch(/unknown|inactive|expired|no such/i);
  });

  it('moves focus to the error summary when sign-in fails', async () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 401, message: 'Sign-in failed', fieldErrors: {} });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(document.activeElement).toBe(root.querySelector('#si-error'));
  });

  it('says how long to wait when sign-in is throttled, and nothing else about why', async () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 429, message: 'Too many attempts', fieldErrors: {}, retryAfterSeconds: 90 });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(root.querySelector('#si-error')?.textContent).toContain('Try again in 2 minutes');
    expect(document.activeElement).toBe(root.querySelector('#si-error'));
  });

  it('says seconds when the wait is short', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 429, message: 'Too many attempts', fieldErrors: {}, retryAfterSeconds: 30 });
    fixture.detectChanges();

    expect(root.querySelector('#si-error')?.textContent).toContain('Try again in 30 seconds');
  });

  it('explains that the second step is not available yet, without a session', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('mfaRequired', true);
    fixture.detectChanges();

    expect(root.querySelector('[role="status"], [role="alert"]')?.textContent).toContain('Two-step verification');
  });

  it('disables the button while signing in, so a second click cannot send a second attempt', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('signingIn', true);
    fixture.detectChanges();

    expect((root.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('clears the password field after a failed attempt', () => {
    const { fixture, root } = create();
    typeInto(root, '#si-password', 'wrong-one');

    fixture.componentRef.setInput('error', { status: 401, message: 'Sign-in failed', fieldErrors: {} });
    fixture.detectChanges();

    expect((root.querySelector('#si-password') as HTMLInputElement).value).toBe('');
  });
});
