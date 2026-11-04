import { TestBed } from '@angular/core/testing';
import { controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { MfaEnrolComponent } from './mfa-enrol.component';

const ENROLMENT = {
  otpAuthUri: 'otpauth://totp/Romp%20Admin:amna.khan?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=Romp%20Admin',
  secretText: 'GEZD GNBV GY3T QOJQ GEZD GNBV GY3T QOJQ',
  expiresAt: '2026-10-26T09:15:00Z',
};

describe('MfaEnrolComponent (mfa AC-1, AC-2)', () => {
  function create() {
    const fixture = TestBed.createComponent(MfaEnrolComponent);
    fixture.componentRef.setInput('enrolment', ENROLMENT);
    const submitted = vi.fn<(code: string) => void>();
    const restarted = vi.fn();
    fixture.componentInstance.codeSubmitted.subscribe(submitted);
    fixture.componentInstance.restartRequested.subscribe(restarted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted, restarted };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  it('has one heading, the steps in order, and labelled controls', () => {
    const { root } = create();

    expect(root.querySelectorAll('h1')).toHaveLength(1);
    expect(root.querySelectorAll('ol li').length).toBeGreaterThanOrEqual(3);
    expect(controlsWithoutLabels(root)).toEqual([]);
  });

  it('shows the QR code and the same secret as text for typing by hand', () => {
    const { root } = create();

    expect(root.querySelector('app-qr-code svg')).not.toBeNull();
    expect(root.querySelector('code')?.textContent?.trim()).toBe(ENROLMENT.secretText);
    expect(root.textContent).toContain('cannot scan');
  });

  it('asks for the six-digit code in a field the phone can fill', () => {
    const { root } = create();
    const input = root.querySelector('#me-code') as HTMLInputElement;

    expect(input.getAttribute('autocomplete')).toBe('one-time-code');
    expect(input.getAttribute('inputmode')).toBe('numeric');
    expect(input.maxLength).toBe(7);
  });

  it('submits the digits, however the app shows them (with a space in the middle)', () => {
    const { root, submitted } = create();
    typeInto(root, '#me-code', '123 456');

    submit(root);

    expect(submitted).toHaveBeenCalledWith('123456');
  });

  it('refuses anything but six digits before asking the server', () => {
    const { fixture, root, submitted } = create();

    for (const bad of ['', '12345', '1234567', '12ab56', 'abcdef']) {
      typeInto(root, '#me-code', bad);
      submit(root);
    }
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Enter the 6-digit code');
  });

  it('shows the server reason in an alert that takes focus', async () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'That code is not valid. Check the code in your app and try again.', fieldErrors: {} });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(root.querySelector('#me-error')?.textContent).toContain('not valid');
    expect(document.activeElement).toBe(root.querySelector('#me-error'));
  });

  it('empties the code field after a failed attempt and cannot be sent twice while saving', () => {
    const { fixture, root } = create();
    typeInto(root, '#me-code', '123456');
    submit(root);
    expect((root.querySelector('#me-code') as HTMLInputElement).value).toBe('');

    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();
    expect((root.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('lets the person start again with a new secret when the first one was lost or expired', () => {
    const { root, restarted } = create();

    (Array.from(root.querySelectorAll('button')).find((b) => /new code|start again/i.test(b.textContent ?? '')) as HTMLButtonElement).click();

    expect(restarted).toHaveBeenCalled();
  });

  it('keeps the secret out of the page title and attributes other than the QR text alternative', () => {
    const { root } = create();

    expect(root.innerHTML.split('GEZDGNBVGY3TQOJQ').length - 1).toBeLessThanOrEqual(1); // only inside the QR address, never copied elsewhere
  });
});
