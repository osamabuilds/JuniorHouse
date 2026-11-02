import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, PLATFORM_ID, RESPONSE_INIT, afterNextRender, effect, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { CartActions } from '../../../../cart/carts/store/cart.actions';
import { PlacedOrder } from '../../../checkout/models/checkout.model';
import { CodResponseComponent } from '../../../cod-confirm/components/cod-response/cod-response.component';
import { CodAnswer } from '../../../cod-confirm/models/cod.model';
import { CodActions } from '../../../cod-confirm/store/cod.actions';
import { selectCodFailed, selectCodLink, selectCodOutcome, selectCodSending, selectCodStatus } from '../../../cod-confirm/store/cod.reducer';

/**
 * The page after an order is placed (design system: OrderConfirmation). The number comes from the address; the details come from the checkout
 * page through the navigation state, so a reload or a Back still shows the number and what happens next without placing anything again (CHK-10).
 * The bag empties on the server a moment after the order, so the header count is refreshed here. `noindex`, never cached.
 */
@Component({
  selector: 'rp-order-placed-page',
  imports: [RouterLink, DecimalPipe, ButtonDirective, IconComponent, CodResponseComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-placed-page.container.html',
})
export class OrderPlacedPageContainer {
  private readonly store = inject(Store);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);

  protected readonly number = inject(ActivatedRoute).snapshot.paramMap.get('number') ?? '';
  protected readonly placed: PlacedOrder | null = (inject(Router).getCurrentNavigation()?.extras.state?.['placed'] as PlacedOrder | undefined) ?? null;

  protected readonly codStatus = this.store.selectSignal(selectCodStatus);
  protected readonly codLink = this.store.selectSignal(selectCodLink);
  protected readonly codSending = this.store.selectSignal(selectCodSending);
  protected readonly codOutcome = this.store.selectSignal(selectCodOutcome);
  protected readonly codFailed = this.store.selectSignal(selectCodFailed);

  protected answer(answer: CodAnswer): void {
    if (this.placed?.confirmToken) this.store.dispatch(CodActions.answered({ token: this.placed.confirmToken, answer }));
  }

  constructor() {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Cache-Control', 'private, no-store');
      this.responseInit.headers = headers;
    }

    effect(() =>
      this.seo.set({ title: 'Thank you for your order', description: 'Your order has been placed.', path: `/order-placed/${this.number}`, siteName: this.brand().name, crumbs: [], noindex: true }),
    );

    const browser = isPlatformBrowser(inject(PLATFORM_ID));
    afterNextRender(() => {
      if (!browser) return;
      // The bag is emptied by the order event within moments: ask a couple of times so the header count settles.
      this.store.dispatch(CartActions.load());
      if (this.placed?.confirmToken) this.store.dispatch(CodActions.statusRequested({ token: this.placed.confirmToken }));
      setTimeout(() => this.store.dispatch(CartActions.load()), 2500);
      setTimeout(() => this.store.dispatch(CartActions.load()), 7000);
    });
  }
}
