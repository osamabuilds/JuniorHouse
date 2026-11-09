import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, PLATFORM_ID, RESPONSE_INIT, afterNextRender, computed, effect, inject, untracked } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { CartActions } from '../../../../cart/carts/store/cart.actions';
import { selectBrand } from '../../../../storefront/site-settings';
import { PaymentActions } from '../../store/payment.actions';
import { MAX_POLLS } from '../../store/payment.effects';
import { selectPaymentPayment, selectPaymentPolls, selectPaymentStart, selectPaymentStarting, selectPaymentStatus } from '../../store/payment.reducer';

/** Where a shopper may be sent to pay: a path on this shop (the pretend gateway) or an https address (a real gateway). Never anything else. */
export function isSafePaymentUrl(url: string | null): url is string {
  return !!url && ((url.startsWith('/') && !url.startsWith('//')) || /^https:\/\/[^\s/]+/.test(url));
}

/**
 * Both ends of an online payment, in one page (orders payments Y3). On `/checkout/pay` it opens the gateway's hosted page and sends the
 * shopper there (never two pages for one order: the server hands back the one already open). On `/checkout/payment-return` it follows the
 * stored state of the payment until it settles and says what happened: the browser coming back is only a hint, so "paid" is shown only when the
 * server says so (PAY-10). The token in the address names one order and nothing else. `noindex`, never cached.
 */
@Component({
  selector: 'rp-payment-page',
  imports: [RouterLink, DecimalPipe, ButtonDirective, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './payment-page.container.html',
})
export class PaymentPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly route = inject(ActivatedRoute);
  private readonly brand = this.store.selectSignal(selectBrand);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  /** `start` on the pay page, `return` when the gateway sends the shopper back. */
  protected readonly mode = (this.route.snapshot.data['mode'] as 'start' | 'return' | undefined) ?? 'return';
  protected readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  protected readonly starting = this.store.selectSignal(selectPaymentStarting);
  protected readonly start = this.store.selectSignal(selectPaymentStart);
  protected readonly status = this.store.selectSignal(selectPaymentStatus);
  protected readonly payment = this.store.selectSignal(selectPaymentPayment);
  private readonly polls = this.store.selectSignal(selectPaymentPolls);

  /** The page the shopper is being sent to, once the server has opened it. */
  protected readonly redirectUrl = computed(() => {
    const s = this.start();
    return s?.status === 'ok' && isSafePaymentUrl(s.redirectUrl) ? s.redirectUrl : null;
  });

  /** After a couple of minutes of "confirming" the page stops asking and says so, rather than spinning for ever. */
  protected readonly gaveUp = computed(() => this.payment()?.state === 'confirming' && this.polls() >= MAX_POLLS);

  /** Why a start did not open a page, when it did not. */
  protected readonly startKind = computed(() => {
    const s = this.start();
    return s?.status === 'ok' && !isSafePaymentUrl(s.redirectUrl) ? s.kind : null;
  });

  constructor() {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Cache-Control', 'private, no-store');
      this.responseInit.headers = headers;
    }

    effect(() =>
      this.seo.set({
        title: this.mode === 'start' ? 'Secure payment' : 'Confirming your payment',
        description: 'Pay for your order.',
        path: this.mode === 'start' ? '/checkout/pay' : '/checkout/payment-return',
        siteName: this.brand().name,
        crumbs: [],
        noindex: true,
      }),
    );

    // The shopper is sent on to the hosted payment page as soon as the server has opened it.
    effect(() => {
      const url = this.redirectUrl();
      if (url && this.browser) untracked(() => window.location.assign(url));
    });

    // Already paid: the return page tells the truth, so go there.
    effect(() => {
      const s = this.start();
      if (this.mode === 'start' && s?.status === 'ok' && s.kind === 'AlreadyPaid') {
        untracked(() => void this.router.navigate(['/checkout/payment-return'], { queryParams: { token: this.token }, replaceUrl: true }));
      }
    });

    // A paid order empties the bag on the server within moments: refresh the header count.
    effect(() => {
      if (this.payment()?.state === 'paid') untracked(() => this.store.dispatch(CartActions.load()));
    });

    // A cash on delivery order has nothing to pay online: send it to its confirmation.
    effect(() => {
      const p = this.payment();
      if (p?.state === 'cod') untracked(() => void this.router.navigate(['/order-placed', p.orderNumber], { replaceUrl: true }));
    });

    afterNextRender(() => {
      if (!this.browser || this.token === '') return;
      this.store.dispatch(this.mode === 'start' ? PaymentActions.startRequested({ token: this.token }) : PaymentActions.statusRequested({ token: this.token }));
    });

    inject(DestroyRef).onDestroy(() => this.store.dispatch(PaymentActions.reset()));
  }

  /** From the return page after a decline: go back to the pay page, which opens a new try. */
  protected retry(): void {
    void this.router.navigate(['/checkout/pay'], { queryParams: { token: this.token } });
  }

  /** From the pay page when the gateway could not be reached: ask again. */
  protected tryAgain(): void {
    if (this.token !== '') this.store.dispatch(PaymentActions.startRequested({ token: this.token }));
  }
}
