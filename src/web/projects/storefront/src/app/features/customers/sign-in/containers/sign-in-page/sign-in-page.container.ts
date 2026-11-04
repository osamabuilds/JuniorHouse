import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { CustomerAuthActions, selectAuthStatus, selectSignInForm } from '../../../../../core/customer-auth';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { AuthLayoutComponent } from '../../components/auth-layout/auth-layout.component';
import { PasswordFieldComponent } from '../../components/password-field/password-field.component';
import { TurnstileComponent } from '../../components/turnstile/turnstile.component';
import { setUpAuthPage } from '../../utils/auth-page.util';
import { safeReturnUrl } from '../../utils/return-url.util';

/**
 * Sign in (ACT-4, ACT-5, ACT-9). A failure always reads "We could not sign you in", whatever was wrong; after repeated failures the API asks
 * for the Turnstile check and the page shows it (SEC-02). Nothing here ever locks the customer out.
 */
@Component({
  selector: 'rp-sign-in-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, FieldComponent, IconComponent, AuthLayoutComponent, PasswordFieldComponent, TurnstileComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sign-in-page.container.html',
})
export class SignInPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly notice = viewChild<ElementRef<HTMLElement>>('notice');
  private readonly captcha = viewChild(TurnstileComponent);

  protected readonly page = setUpAuthPage({ title: 'Sign in', description: 'Sign in to your Romp account.', path: '/account/sign-in' });
  protected readonly state = this.store.selectSignal(selectSignInForm);
  private readonly status = this.store.selectSignal(selectAuthStatus);
  protected readonly returnUrl = toSignal(this.route.queryParamMap.pipe(map((q) => safeReturnUrl(q.get('returnUrl')))), { initialValue: '/account' });

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    keepSignedIn: new FormControl(false, { nonNullable: true }),
  });
  protected submitted = false;
  protected readonly captchaMissing = signal(false);
  private captchaToken: string | null = null;

  constructor() {
    this.store.dispatch(CustomerAuthActions.formOpened());

    // Signed in (by this form): go back to where the customer was.
    effect(() => {
      if (this.status() === 'signedIn') void this.router.navigateByUrl(this.returnUrl());
    });

    // A failure moves focus to the notice, so a screen reader hears it and a keyboard user is not left behind (WCAG 3.3.1).
    effect(() => {
      const s = this.state();
      if (s.failed || s.error) queueMicrotask(() => this.notice()?.nativeElement.focus());
    });
  }

  protected emailError(): string {
    const c = this.form.controls.email;
    if (!this.submitted && !c.touched) return '';
    return c.errors?.['required'] ? 'Enter the email address you registered with.' : c.errors?.['email'] ? 'Check the email address: it looks like something is missing.' : '';
  }

  protected passwordError(): string {
    const c = this.form.controls.password;
    return (this.submitted || c.touched) && c.errors?.['required'] ? 'Enter your password.' : '';
  }

  protected onToken(token: string | null): void {
    this.captchaToken = token;
  }

  protected submit(): void {
    this.submitted = true;
    if (this.form.invalid || this.state().busy) return;
    const needsCaptcha = this.state().captchaRequired;
    this.captchaMissing.set(needsCaptcha && !this.captchaToken);
    if (this.captchaMissing()) return;
    const { email, password, keepSignedIn } = this.form.getRawValue();
    this.store.dispatch(CustomerAuthActions.signInSubmitted({ input: { email: email.trim(), password, keepSignedIn, captchaToken: needsCaptcha ? this.captchaToken : null } }));
    // A token works once, so the next attempt needs a fresh one.
    this.captchaToken = null;
    queueMicrotask(() => this.captcha()?.reset());
  }
}
