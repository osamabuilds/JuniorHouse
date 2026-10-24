import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CatalogProduct, productUrl } from '../data/mock-catalog';
import { GarmentComponent } from '../garment/garment.component';
import { IconComponent } from '../ui/icon/icon.component';

/** Product card (design system ProductCard): 4:5 media with a second view on hover, wishlist heart, name, meta and price. */
@Component({
  selector: 'rp-product-card',
  imports: [DecimalPipe, RouterLink, GarmentComponent, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="rp-card">
      <div class="rp-media">
        <div class="rp-swipe">
          <div class="rp-slide">
            <div class="rp-zoom" style="position: absolute; inset: 0">
              <div class="rp-ph rp-ph--{{ product().tone }}" role="img" [attr.aria-label]="product().name + ', front view (placeholder)'">
                <rp-garment [shape]="product().shape" />
              </div>
            </div>
          </div>
          <div class="rp-slide rp-slide--alt">
            <div class="rp-ph rp-ph--{{ product().altTone }} rp-ph--close" role="img" [attr.aria-label]="product().name + ', close view (placeholder)'">
              <rp-garment [shape]="product().shape" />
            </div>
          </div>
        </div>
        @if (product().tag; as tag) {
          <span class="rp-tag" [class.rp-tag--low]="tag === 'Low stock'">{{ tag }}</span>
        }
        <span class="rp-pagedots" aria-hidden="true"><i></i><i></i></span>
        <button type="button" class="rp-wish" [attr.aria-pressed]="wished()" [attr.aria-label]="'Add ' + product().name + ' to wishlist'" (click)="wished.set(!wished())">
          <rp-icon name="heart" />
        </button>
      </div>
      <div class="rp-card__body">
        <h3 class="rp-card__name"><a [routerLink]="url()">{{ product().name }}</a></h3>
        <p class="rp-card__meta">{{ product().audience === 'girls' ? 'Girls' : 'Boys' }} · {{ product().ages }}</p>
        <span class="rp-price">Rs. {{ product().price | number }}</span>
      </div>
    </article>
  `,
})
export class ProductCardComponent {
  readonly product = input.required<CatalogProduct>();
  protected readonly wished = signal(false);
  protected readonly url = computed(() => productUrl(this.product()));
}
