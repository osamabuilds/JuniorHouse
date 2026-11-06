import { Directive, ElementRef, OnDestroy, inject } from '@angular/core';

const FOCUSABLE = 'summary, input:not([disabled]):not([type="hidden"]), select:not([disabled]), button:not([disabled]), a[href], textarea:not([disabled])';
const PHONE = '(max-width: 63.99rem)';

/**
 * What CSS cannot do for the phone "Filter and sort" sheet (FLT-3, FLT-10). The sheet is a native `<details>`, so it works with no scripting and
 * opens in place; with scripting and a phone-sized screen this adds the dialog behaviour: focus moves into the sheet, Tab stays inside, Escape
 * and a click on the scrim close it and return focus to the summary, and the page behind does not scroll. The scrim is the details element's own
 * backdrop (drawn by CSS), so a click that lands on the element itself is a click outside the sheet.
 */
@Directive({
  selector: 'details[rpSheet]',
  host: { '(toggle)': 'onToggle()', '(keydown)': 'onKey($event)', '(click)': 'onClick($event)' },
})
export class RpSheet implements OnDestroy {
  private readonly el = inject<ElementRef<HTMLDetailsElement>>(ElementRef).nativeElement;
  private locked = false;

  private get active(): boolean {
    return typeof matchMedia !== 'undefined' && matchMedia(PHONE).matches;
  }

  protected onToggle(): void {
    if (!this.active) return;
    if (this.el.open) {
      this.lock();
      this.focusables().find((e) => e.tagName !== 'SUMMARY')?.focus();
    } else {
      this.unlock();
    }
  }

  protected onKey(event: KeyboardEvent): void {
    if (!this.el.open || !this.active) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
      return;
    }
    if (event.key !== 'Tab') return;
    const items = this.focusables();
    if (items.length === 0) return;
    const first = items[0];
    const last = items[items.length - 1];
    const current = document.activeElement;
    if (event.shiftKey && current === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && current === last) {
      event.preventDefault();
      first.focus();
    }
  }

  protected onClick(event: MouseEvent): void {
    if (this.active && this.el.open && event.target === this.el) this.close();
  }

  private close(): void {
    this.el.open = false;
    this.unlock();
    this.el.querySelector<HTMLElement>('summary')?.focus();
  }

  private focusables(): HTMLElement[] {
    return [...this.el.querySelectorAll<HTMLElement>(FOCUSABLE)];
  }

  private lock(): void {
    if (this.locked) return;
    this.locked = true;
    document.body.classList.add('rp-no-scroll');
  }

  private unlock(): void {
    if (!this.locked) return;
    this.locked = false;
    document.body.classList.remove('rp-no-scroll');
  }

  /** A sheet destroyed while open (a route change) must not leave the page locked. */
  ngOnDestroy(): void {
    this.unlock();
  }
}
