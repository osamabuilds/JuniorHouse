import { TestBed } from '@angular/core/testing';
import { buttonNamed, controlsWithoutLabels, setChecked } from '@app/testing/dom-helpers';
import { RecoveryCodesComponent } from './recovery-codes.component';

const CODES = ['ABCDE-FGHJK', 'LMNPQ-RSTUV', 'WXYZ2-34567', 'ABCDE-89ABC', 'DEFGH-JKLMN', 'PQRST-UVWXY', 'Z2345-6789A', 'BCDEF-GHJKL', 'MNPQR-STUVW', 'XYZ23-45678'];

describe('RecoveryCodesComponent (mfa AC-6)', () => {
  function create() {
    const fixture = TestBed.createComponent(RecoveryCodesComponent);
    fixture.componentRef.setInput('codes', CODES);
    const saved = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, saved };
  }

  it('lists all ten codes and says they are shown only once', () => {
    const { root } = create();

    expect(root.querySelectorAll('h1')).toHaveLength(1);
    expect(Array.from(root.querySelectorAll('ol li')).map((li) => li.textContent?.trim())).toEqual(CODES);
    expect(root.textContent).toContain('only time');
    expect(controlsWithoutLabels(root)).toEqual([]);
  });

  it('cannot be dismissed until the person says they saved the codes', () => {
    const { fixture, root, saved } = create();
    const done = buttonNamed(root, 'Continue');
    expect(done.disabled).toBe(true);

    setChecked(root, 'input[type="checkbox"]', true);
    fixture.detectChanges();
    expect(done.disabled).toBe(false);

    done.click();
    expect(saved).toHaveBeenCalledTimes(1);
  });

  it('copies the codes, one per line, and says so politely for screen readers', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const { fixture, root } = create();

    buttonNamed(root, 'Copy codes').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(writeText).toHaveBeenCalledWith(CODES.join('\n'));
    expect(root.querySelector('[role="status"]')?.textContent).toContain('Copied');
  });

  it('does not put the codes anywhere but the list: no attribute, title or link carries them', () => {
    const { root } = create();

    const outsideList = Array.from(root.querySelectorAll('*'))
      .filter((el) => !el.closest('ol'))
      .map((el) => Array.from(el.attributes).map((a) => a.value).join(' '))
      .join(' ');
    expect(CODES.some((code) => outsideList.includes(code))).toBe(false);
  });
});
