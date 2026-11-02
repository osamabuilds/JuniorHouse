import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The figures under a basket, all the server's (SEC-09): subtotal, any code's discount on its own row, delivery (worked out on the order
 * page from the city, so here it says so, TOT-4) and the estimated total. The region is polite-live, so a change to a quantity or a code
 * is announced without moving focus (CRT-1).
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
      @if (discount() > 0) {
        <div class="rp-sumrow rp-sumrow--sale">
          <dt>Discount{{ discountCode() ? ' (' + discountCode() + ')' : '' }}</dt>
          <dd>&minus; Rs. {{ discount() | number }}</dd>
        </div>
      }
      <div class="rp-sumrow">
        <dt>Delivery</dt>
        <dd>{{ delivery() === null ? 'Calculated at checkout' : delivery() === 0 ? 'Free' : 'Rs. ' + (delivery() | number) }}</dd>
      </div>
      <div class="rp-sumrow rp-sumrow--total">
        <dt>Estimated total</dt>
        <dd>Rs. {{ total() | number }}</dd>
      </div>
    </dl>
  `,
})
export class CartSummaryComponent {
  readonly subtotal = input.required<number>();
  readonly itemCount = input.required<number>();
  readonly discount = input(0);
  readonly discountCode = input<string | null>(null);
  readonly delivery = input<number | null>(null);
  /** The server's total; with no figure given it is the subtotal. */
  readonly total = input.required<number>();
}
