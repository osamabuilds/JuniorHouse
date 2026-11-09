import { isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, PLATFORM_ID, RESPONSE_INIT, afterNextRender, effect, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { PromoCodeComponent } from '../../../discount-codes/components/promo-code/promo-code.component';
import { CartLineComponent } from '../../components/cart-line/cart-line.component';
import { CartSummaryComponent } from '../../components/cart-summary/cart-summary.component';
import { CartActions } from '../../store/cart.actions';
import { selectCart, selectCartBusy, selectCartCodeBusy, selectCartCodeError, selectCartIsEmpty, selectCartMessage, selectCartStatus } from '../../store/cart.selectors';

/**
 * The bag as a page (design system: CartPage, CartEmpty). Lines on the left, the summary on the right (stacked on a phone). Quantity changes
 * and Remove update in place and are announced politely. The page is for one person, so it is `noindex` and its response is never cached
 * (SEO-05). Checkout opens with the next sprint; until then the button says so instead of leading nowhere.
 */
@Component({
  selector: 'rp-cart-page',
  imports: [RouterLink, ButtonDirective, IconComponent, CartLineComponent, CartSummaryComponent, PromoCodeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cart-page.container.html',
})
export class CartPageContainer {
  private readonly store = inject(Store);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);

  protected readonly cart = this.store.selectSignal(selectCart);
  protected readonly empty = this.store.selectSignal(selectCartIsEmpty);
  protected readonly status = this.store.selectSignal(selectCartStatus);
  protected readonly busy = this.store.selectSignal(selectCartBusy);
  protected readonly message = this.store.selectSignal(selectCartMessage);
  protected readonly codeBusy = this.store.selectSignal(selectCartCodeBusy);
  protected readonly codeError = this.store.selectSignal(selectCartCodeError);

  constructor() {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Cache-Control', 'private, no-store');
      this.responseInit.headers = headers;
    }

    effect(() =>
      this.seo.set({ title: 'Your bag', description: 'The items in your bag.', path: '/cart', siteName: this.brand().name, crumbs: [], noindex: true }),
    );

    // The basket comes from the browser's own cookies, so it is read there (the server render shows the loading line).
    const browser = isPlatformBrowser(inject(PLATFORM_ID));
    afterNextRender(() => {
      if (browser) this.store.dispatch(CartActions.load());
    });
  }

  protected setQuantity(variantId: number, quantity: number): void {
    this.store.dispatch(CartActions.setQuantity({ variantId, quantity }));
  }

  protected remove(variantId: number): void {
    this.store.dispatch(CartActions.remove({ variantId }));
  }

  protected applyCode(code: string): void {
    this.store.dispatch(CartActions.applyCode({ code }));
  }

  protected removeCode(): void {
    this.store.dispatch(CartActions.removeCode());
  }

  protected retry(): void {
    this.store.dispatch(CartActions.load());
  }
}
