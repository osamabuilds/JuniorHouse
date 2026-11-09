import { TestBed } from '@angular/core/testing';
import { controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { ChangePasswordComponent, ChangePasswordValue } from './change-password.component';

describe('ChangePasswordComponent (staff-accounts AC-5, AC-6; sessions AC-13)', () => {
  const GOOD = 'a-new-long-passphrase-2';

  function create() {
    const fixture = TestBed.createComponent(ChangePasswordComponent);
    const submitted = vi.fn<(value: ChangePasswordValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  function fill(root: HTMLElement, current: string, next: string, confirm = next) {
    typeInto(root, '#cp-current', current);
    typeInto(root, '#cp-new', next);
    typeInto(root, '#cp-confirm', confirm);
  }

  it('has one heading and three labelled password fields with the right autocomplete hints', () => {
    const { root } = create();

    expect(root.querySelectorAll('h1')).toHaveLength(1);
    expect(controlsWithoutLabels(root)).toEqual([]);
    expect(root.querySelector('#cp-current')?.getAttribute('autocomplete')).toBe('current-password');
    expect(root.querySelector('#cp-new')?.getAttribute('autocomplete')).toBe('new-password');
    expect(root.querySelector('#cp-confirm')?.getAttribute('autocomplete')).toBe('new-password');
    expect(root.querySelectorAll('input[type="password"]')).toHaveLength(3);
  });

  it('tells the person the rule up front', () => {
    const { root } = create();

    expect(root.textContent).toContain('at least 12 characters');
  });

  it('submits the current and the new password', () => {
    const { root, submitted } = create();
    fill(root, 'temporary-one', GOOD);

    submit(root);

    expect(submitted).toHaveBeenCalledWith({ currentPassword: 'temporary-one', newPassword: GOOD });
  });

  it('refuses a short password, a mismatch, an empty field and an unchanged password before asking the server', () => {
    const { fixture, root, submitted } = create();

    fill(root, 'temporary-one', 'short');
    submit(root);
    fill(root, 'temporary-one', GOOD, 'something-else-entirely');
    submit(root);
    fill(root, '', GOOD);
    submit(root);
    fill(root, GOOD, GOOD);
    submit(root);
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(root.textContent).toContain("can't be changed yet");
  });

  it('shows the reason the server gave, in an alert that takes focus', async () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 400, message: 'That password is on a list of common passwords.', fieldErrors: {} });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(root.querySelector('#cp-error')?.textContent).toContain('common passwords');
    expect(document.activeElement).toBe(root.querySelector('#cp-error'));
  });

  it('empties the password fields after a rejection and while the change is saving it cannot be sent twice', () => {
    const { fixture, root } = create();
    fill(root, 'temporary-one', GOOD);
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();
    expect((root.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);

    fixture.componentRef.setInput('saving', false);
    fixture.componentRef.setInput('error', { status: 400, message: 'Nope.', fieldErrors: {} });
    fixture.detectChanges();

    expect((root.querySelector('#cp-new') as HTMLInputElement).value).toBe('');
    expect((root.querySelector('#cp-current') as HTMLInputElement).value).toBe('');
  });

  it('shows a notice when the account was told to change its password (a temporary one)', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('forced', true);
    fixture.detectChanges();

    expect(root.textContent).toContain('You need to choose your own password');
  });
});
