import { isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, PLATFORM_ID, RESPONSE_INIT, afterNextRender, effect, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { CustomerAuthApi } from '../../../../../core/customer-auth/services/customer-auth-api.service';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { TurnstileComponent } from '../../../../customers/sign-in/components/turnstile/turnstile.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { TrackedOrderComponent } from '../../components/tracked-order/tracked-order.component';
import { OrderLookupActions } from '../../store/order-lookup.actions';
import { selectLookupCaptchaRequired, selectLookupLinkFailed, selectLookupOrder, selectLookupStatus } from '../../store/order-lookup.reducer';

/**
 * Track your order (FR-37, OLK-1 to OLK-9): a guest types the order number and the mobile number from checkout and sees the status. Every
 * mismatch reads the same, whichever field was wrong (SEC-07). After a few misses the Turnstile check appears (SEC-06). The emailed link
 * (`?token=`) opens the status with no typing (OLK-5). The page is for one person: `noindex`, never cached.
 */
@Component({
  selector: 'rp-track-order-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, FieldComponent, IconComponent, TurnstileComponent, TrackedOrderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-wrap">
      <section class="rp-placed" aria-labelledby="track-heading">
        <div>
          <p class="rp-eyebrow">Order status</p>
          <h1 class="rp-display-lg" id="track-heading">Track your order</h1>
        </div>

        @if (status() === 'busy' && !order()) {
          <p role="status">Looking up your order…</p>
        } @else if (order(); as found) {
          <div aria-live="polite"><rp-tracked-order [order]="found" /></div>
          <div class="rp-placed__actions">
            <button rpBtn="ghost" type="button" (click)="another()">Look up another order</button>
            <a rpBtn="ghost" routerLink="/new-in">Continue shopping</a>
          </div>
        } @else {
          <p class="rp-lead">Enter the order number from your confirmation and the mobile number you gave at checkout.</p>

          @if (linkFailed()) {
            <div #notice class="rp-notice rp-notice--error" role="alert" tabindex="-1">
              <rp-icon name="alert" />
              <div><b>We could not use that link.</b><br />Enter your order number and mobile number below instead.</div>
            </div>
          }
          @if (status() === 'mismatch' || status() === 'captcha' || status() === 'limited' || status() === 'error') {
            <div #notice class="rp-notice rp-notice--error" role="alert" tabindex="-1">
              <rp-icon name="alert" />
              <div>
                @switch (status()) {
                  @case ('captcha') {
                    <b>One more step</b><br />Complete the check below, then find your order again.
                  }
                  @case ('limited') {
                    <b>Too many attempts</b><br />Please wait a while, then try again.
                  }
                  @case ('error') {
                    <b>Something went wrong on our side</b><br />Please try again in a moment.
                  }
                  @default {
                    <b>Those details do not match an order</b><br />Check the order number and the mobile number and try again.
                  }
                }
              </div>
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate aria-label="Track your order">
            <rp-field #number label="Order number" hint="For example RMP-2026-00123-4" [error]="numberError()">
              <input class="rp-input" type="text" name="orderNumber" formControlName="orderNumber" autocomplete="off" autocapitalize="characters" spellcheck="false" required
                [id]="number.controlId" [attr.aria-describedby]="number.describedBy()" [attr.aria-invalid]="number.invalid()" />
            </rp-field>
            <rp-field #mobile label="Mobile number" hint="The number you gave at checkout, for example 0300 1234567" [error]="mobileError()">
              <input class="rp-input" type="tel" name="mobile" formControlName="mobile" autocomplete="tel" inputmode="tel" required
                [id]="mobile.controlId" [attr.aria-describedby]="mobile.describedBy()" [attr.aria-invalid]="mobile.invalid()" />
            </rp-field>

            @if (captchaRequired() && siteKey() !== null) {
              <rp-turnstile [siteKey]="siteKey()!" (token)="onToken($event)" />
              @if (captchaMissing()) {
                <p class="rp-error" role="alert"><rp-icon name="alert" />Complete the check above, then look up your order.</p>
              }
            }

            <button rpBtn="action" type="submit" [block]="true" [disabled]="status() === 'busy'">{{ status() === 'busy' ? 'Looking up…' : 'Find my order' }}</button>
          </form>
          <p class="rp-help">Have an account? <a class="rp-link" routerLink="/account/sign-in">Sign in</a> to see all your orders.</p>
        }
      </section>
    </div>
  `,
})
export class TrackOrderPageContainer {
  private readonly store = inject(Store);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);
  private readonly notice = viewChild<ElementRef<HTMLElement>>('notice');
  private readonly captcha = viewChild(TurnstileComponent);

  /** The `token` query parameter of the address: the emailed link (OLK-5). */
  private readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';

  protected readonly status = this.store.selectSignal(selectLookupStatus);
  protected readonly order = this.store.selectSignal(selectLookupOrder);
  protected readonly captchaRequired = this.store.selectSignal(selectLookupCaptchaRequired);
  protected readonly linkFailed = this.store.selectSignal(selectLookupLinkFailed);
  /** null until the answer is in, so the check never starts before the site key is known. */
  protected readonly siteKey = toSignal(inject(CustomerAuthApi).config().pipe(map((c): string | null => c.turnstileSiteKey)), { initialValue: null });

  protected readonly form = new FormGroup({
    orderNumber: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    mobile: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  protected submitted = false;
  protected readonly captchaMissing = signal(false);
  private captchaToken: string | null = null;

  constructor() {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Cache-Control', 'private, no-store');
      this.responseInit.headers = headers;
    }

    effect(() =>
      this.seo.set({
        title: 'Track your order',
        description: 'Check where your Romp order is with your order number and mobile number.',
        path: '/track-order',
        siteName: this.brand().name,
        crumbs: [],
        noindex: true,
      }),
    );

    this.store.dispatch(OrderLookupActions.formOpened());

    const browser = isPlatformBrowser(inject(PLATFORM_ID));
    afterNextRender(() => {
      if (browser && this.token) this.store.dispatch(OrderLookupActions.linkOpened({ token: this.token }));
    });

    // A failure moves focus to the notice, so a screen reader hears it and a keyboard user is not left behind (WCAG 3.3.1).
    effect(() => {
      if (this.status() === 'mismatch' || this.status() === 'captcha' || this.status() === 'limited' || this.status() === 'error' || this.linkFailed()) queueMicrotask(() => this.notice()?.nativeElement.focus());
    });
  }

  protected numberError(): string {
    const c = this.form.controls.orderNumber;
    return (this.submitted || c.touched) && c.errors?.['required'] ? 'Enter your order number.' : '';
  }

  protected mobileError(): string {
    const c = this.form.controls.mobile;
    return (this.submitted || c.touched) && c.errors?.['required'] ? 'Enter the mobile number you gave at checkout.' : '';
  }

  protected onToken(token: string | null): void {
    this.captchaToken = token;
  }

  protected another(): void {
    this.form.reset();
    this.submitted = false;
    this.store.dispatch(OrderLookupActions.cleared());
  }

  protected submit(): void {
    this.submitted = true;
    if (this.form.invalid || this.status() === 'busy') return;
    const needsCaptcha = this.captchaRequired();
    this.captchaMissing.set(needsCaptcha && !this.captchaToken);
    if (this.captchaMissing()) return;
    const { orderNumber, mobile } = this.form.getRawValue();
    this.store.dispatch(OrderLookupActions.lookupSubmitted({ input: { orderNumber: orderNumber.trim(), mobile: mobile.trim(), captchaToken: needsCaptcha ? this.captchaToken : null } }));
    // A token works once, so the next attempt needs a fresh one.
    this.captchaToken = null;
    queueMicrotask(() => this.captcha()?.reset());
  }
}
