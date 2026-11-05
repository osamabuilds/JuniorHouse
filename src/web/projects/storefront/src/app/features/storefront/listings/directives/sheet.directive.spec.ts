import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RpSheet } from './sheet.directive';

@Component({
  imports: [RpSheet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <details rpSheet>
      <summary id="summary">Filter and sort</summary>
      <form>
        <input id="first" type="checkbox" name="size" value="a" />
        <input id="second" type="checkbox" name="size" value="b" />
        <button id="last" type="submit">Show 5 pieces</button>
      </form>
    </details>
  `,
})
class Host {}

function setup(narrow = true) {
  vi.stubGlobal('matchMedia', () => ({ matches: narrow, addEventListener: () => undefined, removeEventListener: () => undefined }));
  const f = TestBed.createComponent(Host);
  f.detectChanges();
  const el = f.nativeElement as HTMLElement;
  const details = el.querySelector('details') as HTMLDetailsElement;
  const open = () => {
    details.open = true;
    details.dispatchEvent(new Event('toggle'));
  };
  const key = (k: string, shiftKey = false) => details.dispatchEvent(new KeyboardEvent('keydown', { key: k, shiftKey, bubbles: true, cancelable: true }));
  return { f, el, details, open, key, byId: (id: string) => el.querySelector<HTMLElement>('#' + id)! };
}

afterEach(() => {
  vi.unstubAllGlobals();
  document.body.classList.remove('rp-no-scroll');
});

describe('RpSheet', () => {
  it('FLT-10 opening moves focus into the sheet and locks the page scroll', () => {
    const { open, byId } = setup();
    byId('summary').focus();
    open();
    expect(document.activeElement).toBe(byId('first'));
    expect(document.body.classList.contains('rp-no-scroll')).toBe(true);
  });

  it('FLT-10 Escape closes the sheet, unlocks the scroll and returns focus to the summary', () => {
    const { open, key, details, byId } = setup();
    open();
    key('Escape');
    expect(details.open).toBe(false);
    expect(document.activeElement).toBe(byId('summary'));
    expect(document.body.classList.contains('rp-no-scroll')).toBe(false);
  });

  it('FLT-10 Tab from the last control returns to the first, and Shift+Tab from the first goes to the last', () => {
    const { open, key, byId } = setup();
    open();
    byId('last').focus();
    key('Tab');
    expect(document.activeElement).toBe(byId('summary'));
    byId('summary').focus();
    key('Tab', true);
    expect(document.activeElement).toBe(byId('last'));
  });

  it('FLT-10 a click on the scrim (the details element itself) closes it, a click inside does not', () => {
    const { open, details, byId } = setup();
    open();
    byId('first').click();
    expect(details.open).toBe(true);
    details.click();
    expect(details.open).toBe(false);
  });

  it('FLT-10 closing by any route clears the lock, including when the page is destroyed while open', () => {
    const { f, open } = setup();
    open();
    expect(document.body.classList.contains('rp-no-scroll')).toBe(true);
    f.destroy();
    expect(document.body.classList.contains('rp-no-scroll')).toBe(false);
  });

  it('on a wide screen the sheet does not lock the page or take focus (the desktop bar is used instead)', () => {
    const { open, details } = setup(false);
    open();
    expect(details.open).toBe(true);
    expect(document.body.classList.contains('rp-no-scroll')).toBe(false);
  });
});
