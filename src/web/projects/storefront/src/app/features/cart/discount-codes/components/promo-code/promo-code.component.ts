import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';

/**
 * The promo code box and the applied-code chip (design system: CartPage "promo field, applied-code chip"). A code is applied only when the
 * shopper presses Apply (FR-25). Whatever the server says about a refused code is shown under the box in words (FR-26); the code itself is
 * never judged here, so the browser cannot be used to learn which codes exist. One code at a time: while one is applied the box is replaced
 * by the chip, and removing it brings the box back (DSC-4).
 */
@Component({
  selector: 'rp-promo-code',
  imports: [ButtonDirective, FieldComponent, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (appliedCode(); as code) {
      <p class="rp-eyebrow" id="promo-applied">Promo code applied</p>
      <span class="rp-promo-chip" aria-describedby="promo-applied">
        {{ code }}
        <button type="button" [attr.aria-label]="'Remove code ' + code" [disabled]="busy()" (click)="remove.emit()"><rp-icon name="close" /></button>
      </span>
    } @else {
      <form class="rp-promo" (submit)="submit($event)" novalidate>
        <rp-field #promo label="Promo code" [error]="error() ?? ''" class="rp-promo__field">
          <input
            class="rp-input"
            type="text"
            name="promo"
            autocomplete="off"
            autocapitalize="characters"
            spellcheck="false"
            maxlength="32"
            [id]="promo.controlId"
            [attr.aria-describedby]="promo.describedBy()"
            [attr.aria-invalid]="promo.invalid()"
            [value]="draft()"
            (input)="draft.set($any($event.target).value)"
          />
        </rp-field>
        <button rpBtn="ghost" type="submit" [disabled]="busy() || !canApply()">Apply</button>
      </form>
    }
  `,
})
export class PromoCodeComponent {
  /** The code on the bag, if one holds. */
  readonly appliedCode = input<string | null>(null);
  /** The server's reason the last code was refused. */
  readonly error = input<string | null>(null);
  readonly busy = input(false);

  readonly apply = output<string>();
  readonly remove = output<void>();

  protected readonly draft = signal('');
  protected readonly canApply = computed(() => this.draft().trim().length > 0);

  protected submit(event: Event): void {
    event.preventDefault();
    const code = this.draft().trim();
    if (code.length === 0 || this.busy()) return;
    this.apply.emit(code);
  }
}
