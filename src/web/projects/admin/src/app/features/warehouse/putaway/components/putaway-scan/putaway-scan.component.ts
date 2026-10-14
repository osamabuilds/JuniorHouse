import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, effect, input, output, viewChild } from '@angular/core';

/**
 * The barcode box (PUT-14, decision D7): a scanner that behaves like a keyboard types the number and presses Enter, which looks the
 * barcode up. The box takes focus when the page opens and again after each putaway (<c>focusToken</c> changes), so the next scan needs
 * no mouse. Presentational only.
 */
@Component({
  selector: 'app-putaway-scan',
  template: `
    <form class="card" (submit)="submit($event)" aria-labelledby="ps-heading">
      <h2 id="ps-heading">Scan the barcode on the tag</h2>
      <div class="toolbar">
        <label class="toolbar__search">
          <span class="sr-only">Barcode (13 digits)</span>
          <input
            #box
            id="ps-gtin"
            type="text"
            inputmode="numeric"
            autocomplete="off"
            maxlength="13"
            placeholder="Scan or type the 13-digit barcode"
            [attr.aria-describedby]="message() ? 'ps-message' : null"
            [attr.aria-invalid]="message() ? 'true' : null"
          />
        </label>
        <button type="submit" class="button button--primary" [disabled]="scanning()">{{ scanning() ? 'Looking up…' : 'Look up' }}</button>
      </div>
      @if (message()) {
        <p id="ps-message" class="banner banner--error" role="alert">{{ message() }}</p>
      }
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PutawayScanComponent {
  readonly scanning = input(false);
  readonly message = input<string | null>(null);
  readonly focusToken = input(0);

  readonly scanned = output<string>();

  private readonly box = viewChild.required<ElementRef<HTMLInputElement>>('box');

  constructor() {
    afterNextRender(() => this.focus());
    effect(() => {
      this.focusToken();
      this.focus();
    });
  }

  submit(event: Event): void {
    event.preventDefault();
    const input = this.box().nativeElement;
    const value = input.value.trim();
    if (value.length > 0) {
      this.scanned.emit(value);
      input.select();
    }
  }

  private focus(): void {
    this.box().nativeElement.focus();
  }
}
