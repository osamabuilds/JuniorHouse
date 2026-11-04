import { TestBed } from '@angular/core/testing';
import { buttonNamed, controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { MfaChallengeComponent } from './mfa-challenge.component';

describe('MfaChallengeComponent (mfa AC-3, AC-5, AC-6)', () => {
  function create() {
    const fixture = TestBed.createComponent(MfaChallengeComponent);
    const submitted = vi.fn<(code: string) => void>();
    const cancelled = vi.fn();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.componentInstance.cancelled.subscribe(cancelled);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted, cancelled };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  it('asks for the code from the authenticator app, in a field the phone can fill', () => {
    const { root } = create();
    const input = root.querySelector('#mc-code') as HTMLInputElement;

    expect(root.querySelectorAll('h1')).toHaveLength(1);
    expect(input.getAttribute('autocomplete')).toBe('one-time-code');
    expect(input.getAttribute('inputmode')).toBe('numeric');
    expect(controlsWithoutLabels(root)).toEqual([]);
  });

  it('submits the code without spaces', () => {
    const { root, submitted } = create();
    typeInto(root, '#mc-code', ' 123 456 ');

    submit(root);

    expect(submitted).toHaveBeenCalledWith('123456');
  });

  it('refuses an empty or malformed code before asking the server', () => {
    const { fixture, root, submitted } = create();

    for (const bad of ['', '12345', 'abcdef']) {
      typeInto(root, '#mc-code', bad);
      submit(root);
    }
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Enter the 6-digit code');
  });

  it('switches to a recovery code and back, keeping one labelled field', () => {
    const { fixture, root, submitted } = create();

    buttonNamed(root, /recovery code/i).click();
    fixture.detectChanges();
    const input = root.querySelector('#mc-code') as HTMLInputElement;
    expect(root.querySelector('label[for="mc-code"]')?.textContent).toContain('Recovery code');
    expect(input.getAttribute('autocomplete')).toBe('off');
    expect(input.getAttribute('inputmode')).toBeNull();

    typeInto(root, '#mc-code', 'abcde-fghjk');
    submit(root);
    expect(submitted).toHaveBeenCalledWith('abcde-fghjk');

    buttonNamed(root, /authenticator app/i).click();
    fixture.detectChanges();
    expect(root.querySelector('label[for="mc-code"]')?.textContent).toContain('code');
    expect((root.querySelector('#mc-code') as HTMLInputElement).getAttribute('inputmode')).toBe('numeric');
  });

  it('shows one generic message for any wrong code, and moves focus to it', async () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 401, message: 'That code is not valid', fieldErrors: {} });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(root.querySelector('#mc-error')?.textContent).toContain("That code didn't work");
    expect(root.querySelector('#mc-error')?.textContent).not.toMatch(/replay|expired|already used/i);
    expect(document.activeElement).toBe(root.querySelector('#mc-error'));
  });

  it('says how long to wait when throttled', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 429, message: 'Too many attempts', fieldErrors: {}, retryAfterSeconds: 120 });
    fixture.detectChanges();

    expect(root.querySelector('#mc-error')?.textContent).toContain('Try again in 2 minutes');
  });

  it('empties the field as soon as the code is sent and cannot send twice while checking', () => {
    const { fixture, root } = create();
    typeInto(root, '#mc-code', '123456');
    submit(root);
    expect((root.querySelector('#mc-code') as HTMLInputElement).value).toBe('');

    fixture.componentRef.setInput('checking', true);
    fixture.detectChanges();
    expect((root.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('lets the person go back to the password step', () => {
    const { root, cancelled } = create();

    buttonNamed(root, /different account|back/i).click();

    expect(cancelled).toHaveBeenCalled();
  });
});
