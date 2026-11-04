import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Store } from '@ngrx/store';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { WishlistActions } from '../../store/wishlist.actions';
import { selectSavedIds, selectWishlistIdsKnown } from '../../store/wishlist.selectors';

/**
 * The heart on a product tile or product page (WSL-1, WSL-9): a real button, named for the product and its state, with `aria-pressed`.
 * It is drawn off on the server and until the saved ids are known, so the cached HTML stays the same for everyone and a saved product
 * never flashes unsaved (plan, "Cacheability"). `card` sits over a tile picture; `page` is the larger button beside Add to bag.
 */
@Component({
  selector: 'button[rpWishlistHeart]',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    type: 'button',
    '[class.rp-wish]': 'variant() === "card"',
    '[class.rp-heartbtn]': 'variant() === "page"',
    '[attr.aria-pressed]': 'saved()',
    '[attr.aria-label]': 'label()',
    '(click)': 'press($event)',
  },
  template: '<rp-icon name="heart" />',
})
export class WishlistHeartComponent {
  private readonly store = inject(Store);

  readonly productId = input.required<number>();
  /** The product's name, for the accessible name. */
  readonly productName = input.required<string>();
  readonly variant = input<'card' | 'page'>('card');

  private readonly ids = this.store.selectSignal(selectSavedIds);
  private readonly known = this.store.selectSignal(selectWishlistIdsKnown);

  protected readonly saved = computed(() => this.known() && this.ids().includes(this.productId()));
  protected readonly label = computed(() => (this.saved() ? `Remove ${this.productName()} from wishlist` : `Add ${this.productName()} to wishlist`));

  protected press(event: Event): void {
    // The heart sits inside a tile's link area on some layouts; pressing it must never also follow a link.
    event.preventDefault();
    event.stopPropagation();
    this.store.dispatch(WishlistActions.toggle({ productId: this.productId() }));
  }
}
