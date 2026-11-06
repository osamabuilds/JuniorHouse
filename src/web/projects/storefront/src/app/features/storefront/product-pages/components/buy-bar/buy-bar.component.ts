import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { ProductPrice } from '../../models/product-page.model';

/**
 * The bottom buy bar for phones and tablets (design system: ProductDetail; BUY-1..9). Presentational: the product page decides when it shows
 * and what pressing the button does. The price is the server one, exactly as on the page (SEC-09). The hint and message here are visual copies
 * of what the main buying area already announces, so they are hidden from assistive technology to avoid a double announcement.
 */
@Component({
  selector: 'rp-buy-bar',
  imports: [DecimalPipe, ButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-buybar">
      <p class="rp-buybar__price rp-price" [class.rp-price--sale]="price()?.compareAtPrice != null">
        @if (price(); as p) {
          @if (p.isFrom) {
            <span class="rp-buybar__from">From </span>
          }
          <span>Rs. {{ p.price | number }}</span>
          @if (p.compareAtPrice !== null) {
            <s><span class="sr-only">Was </span>Rs. {{ p.compareAtPrice | number }}</s>
          }
        }
      </p>
      <button rpBtn="action" type="button" [disabled]="busy()" (click)="add.emit()">Add to bag</button>
      @if (hint(); as note) {
        <p class="rp-help rp-buybar__note" aria-hidden="true">{{ note }}</p>
      }
      @if (message(); as note) {
        <p class="rp-help rp-buybar__note" aria-hidden="true">{{ note }}</p>
      }
    </div>
  `,
})
export class BuyBarComponent {
  readonly price = input.required<ProductPrice | null>();
  readonly busy = input(false);
  readonly hint = input<string | null>(null);
  readonly message = input<string | null>(null);
  readonly add = output<void>();
}
