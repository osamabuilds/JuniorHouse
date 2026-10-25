import { Directive, ElementRef, OnDestroy, inject, output } from '@angular/core';

let openCount = 0;

/**
 * Behaviour for a native `<dialog>` used as a modal drawer or overlay: `showModal()` (focus trap, inert background,
 * Esc), close on scrim click, body scroll lock, and focus returned to whatever opened it.
 */
@Directive({
  selector: 'dialog[rpDialog]',
  exportAs: 'rpDialog',
  host: {
    '(click)': 'onClick($event)',
    '(cancel)': 'onCancel($event)',
    '(close)': 'onClosed()',
  },
})
export class RpDialog implements OnDestroy {
  private readonly el = inject<ElementRef<HTMLDialogElement>>(ElementRef).nativeElement;
  private opener: HTMLElement | null = null;
  private locked = false;

  readonly closed = output<void>();

  get isOpen(): boolean {
    return this.el.hasAttribute('open');
  }

  open(): void {
    if (this.isOpen) return;
    this.opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.el.showModal();
    this.lock();
  }

  close(): void {
    if (!this.isOpen) return;
    this.el.close();
    this.onClosed();
  }

  protected onClick(event: MouseEvent): void {
    if (event.target === this.el) this.close();
  }

  protected onCancel(event: Event): void {
    event.preventDefault();
    this.close();
  }

  protected onClosed(): void {
    if (!this.locked) return;
    this.unlock();
    this.opener?.focus();
    this.opener = null;
    this.closed.emit();
  }

  /** A dialog destroyed while open (a route change, a closed test fixture) must not leave the page scroll-locked. */
  ngOnDestroy(): void {
    if (this.locked) this.unlock();
  }

  private lock(): void {
    if (this.locked) return;
    this.locked = true;
    openCount++;
    document.body.classList.add('rp-no-scroll');
  }

  private unlock(): void {
    this.locked = false;
    openCount = Math.max(0, openCount - 1);
    if (openCount === 0) document.body.classList.remove('rp-no-scroll');
  }
}
