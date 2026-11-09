import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, effect, input, output, signal, viewChild } from '@angular/core';

/**
 * The one field a keyboard-wedge barcode scanner types into (docs/specs/fulfilment/warehouse-app, APP-3, NFR-SC-07): it takes focus when the
 * screen opens and keeps it after every scan, so the picker never has to click. A scanner types the code and presses Enter, which submits the
 * form. It is presentational: it only reports the code. Every control is at least 44 px tall (APP-5).
 */
@Component({
  selector: 'app-scan-box',
  templateUrl: './scan-box.component.html',
  styleUrl: './scan-box.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScanBoxComponent {
  /** The visible label, for example "Scan a shelf or an item". */
  readonly label = input('Scan a shelf or an item');
  readonly hint = input<string | null>(null);
  /** True while a scan is on its way, or the order cannot take scans. The field is then read-only so focus is not lost. */
  readonly busy = input(false);
  readonly inputId = input('scan-box-input');

  readonly scanned = output<string>();

  protected readonly value = signal('');
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  constructor() {
    afterNextRender(() => this.focus());
    // When a scan finishes the field is ready again: take the focus back.
    effect(() => {
      if (!this.busy()) {
        queueMicrotask(() => this.focus());
      }
    });
  }

  focus(): void {
    this.field().nativeElement.focus();
  }

  protected submit(event: Event): void {
    event.preventDefault();
    const code = this.value().trim();
    this.value.set('');
    // Clear the field itself too: a scanner types and presses Enter faster than a render, so the bound value may not have reached the page yet.
    this.field().nativeElement.value = '';
    if (code && !this.busy()) {
      this.scanned.emit(code);
    }

    this.focus();
  }

  protected type(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }
}
