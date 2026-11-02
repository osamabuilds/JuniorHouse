import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, PLATFORM_ID, RESPONSE_INIT, afterNextRender, computed, effect, inject, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { CartActions } from '../../../../cart/carts/store/cart.actions';
import { CheckoutForm, EMPTY_FORM, FieldErrors, PlacedOrder } from '../../models/checkout.model';
import { CheckoutActions } from '../../store/checkout.actions';
import {
  selectCheckoutCities,
  selectCheckoutPlacing,
  selectCheckoutProvinces,
  selectCheckoutResult,
  selectCheckoutStatus,
  selectCheckoutSummary,
} from '../../store/checkout.reducer';

type FormKey = keyof CheckoutForm;

/** Said when a required box is empty: what to do, not what went wrong (CHK-2). */
const REQUIRED: Readonly<Partial<Record<FormKey, string>>> = {
  name: 'Enter your name.',
  mobile: 'Enter your mobile number, such as 0300 1234567.',
  recipientName: 'Enter the name of the person who will receive the parcel.',
  recipientPhone: 'Enter a phone number so the courier can reach you.',
  lineOne: 'Enter the street address, such as house number and street.',
  provinceId: 'Choose a province.',
  cityId: 'Choose a city.',
};

/**
 * Checkout on one page, guest by default (design system: CheckoutPage): contact, delivery address, payment, then Place order. The slim header
 * keeps the shopper on the task. Every figure is the server's (SEC-09); the page sends only what was typed, with one key made when the page
 * opened, so a double click or Back and resubmit cannot place two orders (CHK-10). Nothing is ever blocked on account state (CHK-6). The page
 * is for one person: `noindex` and never cached (CHK-11).
 */
@Component({
  selector: 'rp-checkout-page',
  imports: [RouterLink, DecimalPipe, FieldComponent, ButtonDirective, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout-page.container.html',
})
export class CheckoutPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly status = this.store.selectSignal(selectCheckoutStatus);
  protected readonly summary = this.store.selectSignal(selectCheckoutSummary);
  protected readonly provinces = this.store.selectSignal(selectCheckoutProvinces);
  protected readonly cities = this.store.selectSignal(selectCheckoutCities);
  protected readonly placing = this.store.selectSignal(selectCheckoutPlacing);
  private readonly result = this.store.selectSignal(selectCheckoutResult);

  protected readonly form = signal<CheckoutForm>(EMPTY_FORM);
  private readonly attempted = signal(false);
  private readonly touched = signal(false);
  private key = '';

  protected readonly savedAddresses = computed(() => this.summary()?.savedAddresses ?? []);
  protected readonly usingSaved = computed(() => this.form().savedAddressId !== null);
  protected readonly selectedMethod = computed(() => this.summary()?.paymentMethods.find((m) => m.code === this.form().paymentMethod) ?? null);

  /** What the server said was wrong (field messages) or why it refused (stock, code), shown beside the boxes and at the top. */
  protected readonly serverErrors = computed((): FieldErrors => {
    const r = this.result();
    return r?.status === 'invalid' ? r.errors : {};
  });
  protected readonly refusal = computed(() => {
    const r = this.result();
    return r?.status === 'refused' ? r.messages : r?.status === 'error' ? ['Something went wrong on our side. Please try again in a moment. You have not been charged.'] : [];
  });

  /** One message per box: the server's if it sent one, else "required" once a send has been tried. */
  protected readonly shown = computed((): Readonly<Partial<Record<FormKey, string>>> => {
    const server = this.serverErrors();
    const f = this.form();
    const out: Partial<Record<FormKey, string>> = {};
    const check = (key: FormKey, empty: boolean): void => {
      const message = server[key] ?? (this.attempted() && empty ? REQUIRED[key] : undefined);
      if (message) out[key] = message;
    };
    check('name', f.name.trim() === '');
    check('mobile', f.mobile.trim() === '');
    if (f.savedAddressId === null) {
      check('recipientName', f.recipientName.trim() === '');
      check('recipientPhone', f.recipientPhone.trim() === '');
      check('lineOne', f.lineOne.trim() === '');
      check('provinceId', f.provinceId === 0);
      check('cityId', f.cityId === 0);
    }
    if (server['email']) out.email = server['email'];
    return out;
  });

  constructor() {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Cache-Control', 'private, no-store');
      this.responseInit.headers = headers;
    }

    effect(() =>
      this.seo.set({ title: 'Checkout', description: 'Secure checkout.', path: '/checkout', siteName: this.brand().name, crumbs: [], noindex: true }),
    );

    // The summary and the saved addresses come from the browser's own cookies, so they are read there.
    afterNextRender(() => {
      if (!this.browser) return;
      this.key = crypto.randomUUID().replace(/-/g, '');
      this.store.dispatch(CheckoutActions.opened());
    });

    // A signed-in customer's default address is chosen for them until they touch anything (CHK-4).
    effect(() => {
      const saved = this.savedAddresses();
      if (saved.length === 0 || untracked(this.touched)) return;
      const chosen = saved.find((a) => a.isDefault) ?? saved[0];
      untracked(() => {
        this.form.update((f) => ({ ...f, savedAddressId: chosen.id }));
        this.store.dispatch(CheckoutActions.summaryRequested({ cityId: chosen.cityId, method: this.form().paymentMethod }));
      });
    });

    effect(() => {
      const r = this.result();
      if (!r) return;
      untracked(() => {
        if (r.status === 'placed') {
          this.finish(r.orderNumber);
        } else {
          if (r.status === 'refused') this.store.dispatch(CartActions.load());
          this.focusFirstProblem();
        }
      });
    });
  }

  protected set<K extends FormKey>(key: K, value: CheckoutForm[K]): void {
    this.touched.set(true);
    this.form.update((f) => ({ ...f, [key]: value }));
  }

  protected setText(key: FormKey, event: Event): void {
    this.set(key, (event.target as HTMLInputElement | HTMLTextAreaElement).value as never);
  }

  protected setChecked(key: FormKey, event: Event): void {
    this.set(key, (event.target as HTMLInputElement).checked as never);
  }

  protected chooseSaved(id: number | null): void {
    this.touched.set(true);
    const saved = this.savedAddresses().find((a) => a.id === id) ?? null;
    this.form.update((f) => ({ ...f, savedAddressId: id }));
    this.store.dispatch(CheckoutActions.summaryRequested({ cityId: saved?.cityId ?? (this.form().cityId || null), method: this.form().paymentMethod }));
  }

  protected chooseProvince(event: Event): void {
    const id = Number((event.target as HTMLSelectElement).value);
    this.touched.set(true);
    this.form.update((f) => ({ ...f, provinceId: id, cityId: 0 }));
    if (id > 0) this.store.dispatch(CheckoutActions.provinceChosen({ provinceId: id }));
  }

  protected chooseCity(event: Event): void {
    const id = Number((event.target as HTMLSelectElement).value);
    this.touched.set(true);
    this.form.update((f) => ({ ...f, cityId: id }));
    this.store.dispatch(CheckoutActions.summaryRequested({ cityId: id || null, method: this.form().paymentMethod }));
  }

  protected chooseMethod(code: string): void {
    this.touched.set(true);
    this.form.update((f) => ({ ...f, paymentMethod: code }));
    const cityId = this.usingSaved() ? (this.savedAddresses().find((a) => a.id === this.form().savedAddressId)?.cityId ?? null) : this.form().cityId || null;
    this.store.dispatch(CheckoutActions.summaryRequested({ cityId, method: code }));
  }

  protected place(event: Event): void {
    event.preventDefault();
    this.attempted.set(true);
    if (this.placing()) return;
    if (Object.keys(this.shown()).length > 0 || !this.summary()?.canOrder) {
      this.focusFirstProblem();
      return;
    }
    this.store.dispatch(CheckoutActions.submitted({ form: this.form(), key: this.key }));
  }

  private finish(orderNumber: string): void {
    const s = this.summary();
    const f = this.form();
    const saved = this.savedAddresses().find((a) => a.id === f.savedAddressId);
    const placed: PlacedOrder = {
      orderNumber,
      total: s?.total ?? 0,
      paymentMethodName: this.selectedMethod()?.name ?? 'Cash on Delivery',
      deliveringTo: saved ? `${saved.recipientName}, ${saved.lineOne}` : `${f.recipientName}, ${f.lineOne}, ${this.cities().find((c) => c.id === f.cityId)?.name ?? ''}`,
      lines: s?.lines ?? [],
      subtotal: s?.subtotal ?? 0,
      discount: s?.discount ?? 0,
      shipping: s?.shipping ?? null,
    };
    void this.router.navigate(['/order-placed', orderNumber], { state: { placed } });
  }

  /** Moves focus to the first message so a keyboard or screen reader user lands on what needs fixing (CHK-11). */
  private focusFirstProblem(): void {
    if (!this.browser) return;
    setTimeout(() => (document.querySelector('[aria-invalid="true"], .rp-notice[role="alert"]') as HTMLElement | null)?.focus());
  }
}
