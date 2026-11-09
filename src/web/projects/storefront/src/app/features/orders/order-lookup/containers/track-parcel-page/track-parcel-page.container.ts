import { ChangeDetectionStrategy, Component, RESPONSE_INIT, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { CustomerAuthApi } from '../../../../../core/customer-auth/services/customer-auth-api.service';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { TurnstileComponent } from '../../../../customers/sign-in/components/turnstile/turnstile.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { ShipmentTrackingComponent } from '../../components/shipment-tracking/shipment-tracking.component';
import { ParcelResult, ShipmentTracking } from '../../models/shipment-tracking.model';
import { ParcelTrackingApi } from '../../services/parcel-tracking-api.service';

/**
 * Track a parcel by its tracking number (TRK-9, TRK-10): a stranger holding only the number sees the courier status timeline and nothing about the
 * order. Every miss reads the same (SEC-07); after a few the Turnstile check appears (SEC-06). The page is for one person: `noindex`, never cached.
 */
@Component({
  selector: 'rp-track-parcel-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, FieldComponent, IconComponent, TurnstileComponent, ShipmentTrackingComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-wrap">
      <section class="rp-placed" aria-labelledby="parcel-track-heading">
        <div>
          <p class="rp-eyebrow">Parcel status</p>
          <h1 class="rp-display-lg" id="parcel-track-heading">Track your parcel</h1>
        </div>

        @if (found(); as tracking) {
          <div aria-live="polite"><rp-shipment-tracking [tracking]="tracking" /></div>
          <div class="rp-placed__actions">
            <button rpBtn="ghost" type="button" (click)="another()">Track another parcel</button>
          </div>
        } @else {
          <p class="rp-lead">Enter the tracking number from your delivery message.</p>

          @if (problem(); as p) {
            <div class="rp-notice rp-notice--error" role="alert">
              <rp-icon name="alert" />
              <div>
                @switch (p) {
                  @case ('captcha') {
                    <b>One more step</b><br />Complete the check below, then track the parcel again.
                  }
                  @case ('limited') {
                    <b>Too many attempts</b><br />Please wait a while, then try again.
                  }
                  @case ('error') {
                    <b>Something went wrong on our side</b><br />Please try again in a moment.
                  }
                  @default {
                    <b>That tracking number does not match a parcel</b><br />Check the number and try again.
                  }
                }
              </div>
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate aria-label="Track your parcel">
            <rp-field #number label="Tracking number" [error]="numberError()">
              <input class="rp-input" type="text" name="waybill" formControlName="waybill" autocomplete="off" autocapitalize="characters" spellcheck="false" required
                [id]="number.controlId" [attr.aria-describedby]="number.describedBy()" [attr.aria-invalid]="number.invalid()" />
            </rp-field>

            @if (captchaRequired() && siteKey() !== null) {
              <rp-turnstile [siteKey]="siteKey()!" (token)="onToken($event)" />
              @if (captchaMissing()) {
                <p class="rp-error" role="alert"><rp-icon name="alert" />Complete the check above, then track the parcel.</p>
              }
            }

            <button rpBtn="action" type="submit" [block]="true" [disabled]="busy()">{{ busy() ? 'Looking up…' : 'Track parcel' }}</button>
          </form>
          <p class="rp-help">Want the whole order? <a class="rp-link" routerLink="/track-order">Track your order</a>.</p>
        }
      </section>
    </div>
  `,
})
export class TrackParcelPageContainer {
  private readonly api = inject(ParcelTrackingApi);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = inject(Store).selectSignal(selectBrand);

  protected readonly siteKey = toSignal(inject(CustomerAuthApi).config().pipe(map((c): string | null => c.turnstileSiteKey)), { initialValue: null });
  protected readonly form = new FormGroup({ waybill: new FormControl('', { nonNullable: true, validators: [Validators.required] }) });
  protected readonly found = signal<ShipmentTracking | null>(null);
  protected readonly problem = signal<Exclude<ParcelResult['kind'], 'found'> | null>(null);
  protected readonly busy = signal(false);
  protected readonly captchaRequired = signal(false);
  protected readonly captchaMissing = signal(false);
  protected submitted = false;
  private captchaToken: string | null = null;

  constructor() {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Cache-Control', 'private, no-store');
      this.responseInit.headers = headers;
    }

    effect(() =>
      this.seo.set({
        title: 'Track your parcel',
        description: 'Check where your Romp parcel is with the tracking number from your delivery message.',
        path: '/track-parcel',
        siteName: this.brand().name,
        crumbs: [],
        noindex: true,
      }),
    );
  }

  protected numberError(): string {
    const c = this.form.controls.waybill;
    return (this.submitted || c.touched) && c.errors?.['required'] ? 'Enter your tracking number.' : '';
  }

  protected onToken(token: string | null): void {
    this.captchaToken = token;
  }

  protected another(): void {
    this.form.reset();
    this.submitted = false;
    this.found.set(null);
    this.problem.set(null);
  }

  protected submit(): void {
    this.submitted = true;
    if (this.form.invalid || this.busy()) return;
    const needsCaptcha = this.captchaRequired();
    this.captchaMissing.set(needsCaptcha && !this.captchaToken);
    if (this.captchaMissing()) return;
    this.busy.set(true);
    this.api.track(this.form.getRawValue().waybill.trim(), needsCaptcha ? this.captchaToken : null).subscribe((result) => {
      this.busy.set(false);
      // A token works once, so the next attempt needs a fresh one.
      this.captchaToken = null;
      if (result.kind === 'found') {
        this.found.set(result.tracking);
        this.problem.set(null);
        return;
      }
      if (result.kind === 'captcha') this.captchaRequired.set(true);
      this.problem.set(result.kind);
    });
  }
}
