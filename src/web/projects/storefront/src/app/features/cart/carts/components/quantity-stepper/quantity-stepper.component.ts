import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MAX_PER_LINE } from '../../models/cart.model';

/** The design system's quantity stepper (`rp-qty`): 44px targets, the item named for assistive technology, a press at 1 asks for 0, which the line turns into a removal. */
@Component({
  selector: 'rp-quantity-stepper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-qty" role="group" [attr.aria-label]="'Quantity of ' + label()">
      <button type="button" [attr.aria-label]="value() <= 1 ? 'Remove from bag' : 'Decrease quantity'" [disabled]="disabled()" (click)="changed.emit(value() - 1)">−</button>
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
