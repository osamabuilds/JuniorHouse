import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The figures under a basket. The subtotal is the server's; delivery is worked out on the order page from the city, so here it says so
 * (TOT-4, Q-TOT-1). The region is polite-live, so a change to a quantity is announced without moving focus (CRT-1).
 */
@Component({
  selector: 'rp-cart-summary',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dl aria-live="polite" aria-atomic="true">
      <div class="rp-sumrow">
        <dt>Subtotal ({{ itemCount() }} {{ itemCount() === 1 ? 'item' : 'items' }})</dt>
        <dd>Rs. {{ subtotal() | number }}</dd>
      </div>
      <div class="rp-sumrow">
        <dt>Delivery</dt>
        <dd>Calculated at checkout</dd>
      </div>
      <div class="rp-sumrow rp-sumrow--total">
        <dt>Estimated total</dt>
        <dd>Rs. {{ subtotal() | number }}</dd>
      </div>
    </dl>
  `,
})
export class CartSummaryComponent {
  readonly subtotal = input.required<number>();
  readonly itemCount = input.required<number>();
}
