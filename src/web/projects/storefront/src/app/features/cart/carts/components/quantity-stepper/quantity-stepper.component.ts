import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MAX_PER_LINE } from '../../models/cart.model';

/** The design system's quantity stepper (`rp-qty`): 44px targets, the item named for assistive technology, never below 1 (CRT-11). */
@Component({
  selector: 'rp-quantity-stepper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-qty" role="group" [attr.aria-label]="'Quantity of ' + label()">
      <button type="button" aria-label="Decrease quantity" [disabled]="disabled() || value() <= 1" (click)="changed.emit(value() - 1)">−</button>
      <span>{{ value() }}</span>
      <button type="button" aria-label="Increase quantity" [disabled]="disabled() || value() >= max()" (click)="changed.emit(value() + 1)">+</button>
    </div>
  `,
})
export class QuantityStepperComponent {
  readonly value = input.required<number>();
  /** The name of the item, for the group's label. */
  readonly label = input.required<string>();
  readonly max = input(MAX_PER_LINE);
  readonly disabled = input(false);
  readonly changed = output<number>();
}
