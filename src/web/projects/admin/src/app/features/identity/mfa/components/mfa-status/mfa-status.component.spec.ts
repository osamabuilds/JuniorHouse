import { TestBed } from '@angular/core/testing';
import { buttonNamed, controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { MfaStatusComponent } from './mfa-status.component';

describe('MfaStatusComponent (mfa AC-6)', () => {
  function create(status = { enrolled: true, recoveryCodesRemaining: 8 }) {
    const fixture = TestBed.createComponent(MfaStatusComponent);
    fixture.componentRef.setInput('status', status);
    const regenerate = vi.fn<(code: string) => void>();
    fixture.componentInstance.regenerateRequested.subscribe(regenerate);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, regenerate };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  it('says two-step verification is on and how many recovery codes are left', () => {
    const { root } = create();

    expect(root.querySelectorAll('h1')).toHaveLength(1);
    expect(root.textContent).toContain('Two-step verification is on');
    expect(root.textContent).toContain('8 recovery codes left');
    expect(controlsWithoutLabels(root)).toEqual([]);
  });

  it('warns when few recovery codes are left, and says "1 recovery code" in the singular', () => {
    const low = create({ enrolled: true, recoveryCodesRemaining: 1 });

    expect(low.root.textContent).toContain('1 recovery code left');
    expect(low.root.querySelector('.banner--warning, [role="status"]')?.textContent).toMatch(/few|running low|get new/i);
    expect(create({ enrolled: true, recoveryCodesRemaining: 9 }).root.querySelector('.banner--warning')).toBeNull();
  });

  it('needs a current code to make new recovery codes, and sends only the digits', () => {
    const { fixture, root, regenerate } = create();

    typeInto(root, '#ms-code', 'abc');
    submit(root);
    fixture.detectChanges();
    expect(regenerate).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Enter the 6-digit code');

    typeInto(root, '#ms-code', '123 456');
    submit(root);
    expect(regenerate).toHaveBeenCalledWith('123456');
    expect((root.querySelector('#ms-code') as HTMLInputElement).value).toBe('');
  });

  it('explains what making new codes does: the old ones stop working', () => {
    const { root } = create();

    expect(root.textContent).toMatch(/old codes stop working/i);
    expect(buttonNamed(root, 'Get new recovery codes')).toBeTruthy();
  });

  it('shows the reason when it fails, in an alert', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'That code is not valid. Wait for the next code in your app and try again.', fieldErrors: {} });
    fixture.detectChanges();

    expect(root.querySelector('[role="alert"]')?.textContent).toContain('not valid');
  });

  it('says so when two-step verification is not set up yet', () => {
    const { root } = create({ enrolled: false, recoveryCodesRemaining: 0 });

    expect(root.textContent).toContain('not set up');
    expect(root.querySelector('form')).toBeNull();
  });
});
