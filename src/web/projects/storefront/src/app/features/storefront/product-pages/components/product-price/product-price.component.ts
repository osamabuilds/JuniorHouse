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
      <p class="rp-price rp-price--lg" [class.rp-price--sale]="p.compareAtPrice !== null">
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
            <span class="rp-off">−{{ p.savingPercent }}%</span>
          }
        }
      </p>
      @if (p.compareAtPrice !== null && p.savingAmount > 0) {
        <p class="rp-save">You save Rs. {{ p.savingAmount | number }}</p>
      }
    }
  `,
  styles: `
    .price__from { color: var(--ink-muted); font-weight: 400; }
    :host { display: grid; gap: var(--space-1); }
    .rp-price, .rp-save { margin: 0; }
  `,
})
export class ProductPriceComponent {
  readonly price = input.required<ProductPrice | null>();
}
