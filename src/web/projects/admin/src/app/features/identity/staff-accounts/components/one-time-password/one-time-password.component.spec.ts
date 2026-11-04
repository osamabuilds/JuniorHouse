import { TestBed } from '@angular/core/testing';
import { temporaryPassword } from '@app/testing/identity-fixtures';
import { buttonNamed } from '@app/testing/dom-helpers';
import { OneTimePasswordComponent } from './one-time-password.component';

describe('OneTimePasswordComponent (spec AC-4)', () => {
  function create() {
    const fixture = TestBed.createComponent(OneTimePasswordComponent);
    fixture.componentRef.setInput('password', temporaryPassword());
    const dismissed = vi.fn<() => void>();
    fixture.componentInstance.dismissed.subscribe(dismissed);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, dismissed };
  }

  afterEach(() => vi.unstubAllGlobals());

  it('shows the password once, with a warning that it will not be shown again', () => {
    const { root } = create();

    expect(root.querySelector('output')?.textContent).toBe('Xk7m-Qp4t-Rv9w-Zc2n');
    expect(root.querySelector('[role="alert"]')?.textContent).toContain('only time');
    expect(root.querySelector('h2')?.textContent).toContain('amna.khan');
  });

  it('copies the password to the clipboard and says so', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    const { fixture, root } = create();

    buttonNamed(root, 'Copy password').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(writeText).toHaveBeenCalledWith('Xk7m-Qp4t-Rv9w-Zc2n');
    expect(root.querySelector('[role="status"]')?.textContent).toContain('Copied');
  });

  it('tells the person to copy by hand when the browser refuses the clipboard', async () => {
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } });
    const { fixture, root } = create();

    buttonNamed(root, 'Copy password').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(root.querySelector('[role="status"]')?.textContent).toContain('by hand');
  });

  it('asks the page to drop the password when the person is done', () => {
    const { root, dismissed } = create();

    buttonNamed(root, /Done/).click();

    expect(dismissed).toHaveBeenCalledOnce();
  });
});
