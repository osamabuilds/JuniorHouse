import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ProductPrice } from '../../models/product-page.model';

/**
 * The price line (PDP-9, 15). Every figure is the server one: the compare-at price is struck through only when the server sent one, and the
 * saving is the server percentage. "From" shows before a size is chosen when sizes differ in price. The struck price is announced as "Was".
 */
@Component({
  selector: 'rp-product-price',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (price(); as p) {
      <p class="rp-price price" [class.rp-price--sale]="p.compareAtPrice !== null">
        @if (p.isFrom) {
          <span class="price__from">From</span>
        }
        @if (p.compareAtPrice !== null) {
          <span class="sr-only">Now </span>
        }
        <span>Rs. {{ p.price | number }}</span>
        @if (p.compareAtPrice !== null) {
          <s><span class="sr-only">Was </span>Rs. {{ p.compareAtPrice | number }}</s>
          @if (p.savingPercent > 0) {
            <span class="price__saving">Save {{ p.savingPercent }}%</span>
          }
        }
      </p>
    }
  `,
  styles: `
    .price { margin: 0; font-size: 18px; }
    .price__from { color: var(--ink-muted); font-weight: 400; }
    .price__saving { font: 500 12px/16px var(--font-sans); letter-spacing: 0.08em; text-transform: uppercase; }
  `,
})
export class ProductPriceComponent {
  readonly price = input.required<ProductPrice | null>();
}
