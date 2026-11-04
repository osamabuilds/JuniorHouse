import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { CustomerAuthActions, selectAuthStatus, selectRegisterForm } from '../../../../../core/customer-auth';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { AuthLayoutComponent } from '../../components/auth-layout/auth-layout.component';
import { PasswordChecklistComponent } from '../../components/password-checklist/password-checklist.component';
import { PasswordFieldComponent } from '../../components/password-field/password-field.component';
import { setUpAuthPage } from '../../utils/auth-page.util';
import { looksLikePakistaniMobile } from '../../utils/mobile.util';
import { isPasswordValid } from '../../utils/password-rules.util';
import { safeReturnUrl } from '../../utils/return-url.util';

/**
 * Create an account (ACT-1, ACT-2, ACT-3, ACT-29). The mobile number is checked for shape and saved, not verified. The password checklist
 * updates as the customer types; the API enforces the same rules and answers by name if it refuses one. Registering signs the customer in.
 */
@Component({
  selector: 'rp-register-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, FieldComponent, IconComponent, AuthLayoutComponent, PasswordFieldComponent, PasswordChecklistComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register-page.container.html',
})
export class RegisterPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly notice = viewChild<ElementRef<HTMLElement>>('notice');

  protected readonly page = setUpAuthPage({ title: 'Create an account', description: 'Create your Romp account.', path: '/account/register' });
  protected readonly state = this.store.selectSignal(selectRegisterForm);
  private readonly status = this.store.selectSignal(selectAuthStatus);
  private readonly returnUrl = toSignal(this.route.queryParamMap.pipe(map((q) => safeReturnUrl(q.get('returnUrl')))), { initialValue: '/account' });

  protected readonly form = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(80)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(80)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    mobile: new FormControl('', { nonNullable: true, validators: [Validators.required, (c) => (!c.value || looksLikePakistaniMobile(c.value) ? null : { mobile: true })] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, (c) => (isPasswordValid(c.value) ? null : { rules: true })] }),
    terms: new FormControl(false, { nonNullable: true, validators: [Validators.requiredTrue] }),
    newsletter: new FormControl(false, { nonNullable: true }),
  });
  protected readonly submitted = signal(false);
  protected readonly typedPassword = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });
  protected readonly anyError = computed(() => this.state().emailTaken || this.state().error || this.state().unmetRules.length > 0);

  constructor() {
    this.store.dispatch(CustomerAuthActions.formOpened());
    effect(() => {
      if (this.status() === 'signedIn') void this.router.navigateByUrl(this.returnUrl());
    });
    effect(() => {
      if (this.anyError()) queueMicrotask(() => this.notice()?.nativeElement.focus());
    });
  }

  /** The first problem a field has, in words that say what to do (or the API's own wording for it). */
  protected error(name: 'firstName' | 'lastName' | 'email' | 'mobile'): string {
    const c = this.form.controls[name];
    const fromApi = this.state().fieldErrors[name];
    if (fromApi) return fromApi;
    if (!this.submitted() && !c.touched) return '';
    if (c.errors?.['required']) {
      return { firstName: 'Enter your first name.', lastName: 'Enter your last name.', email: 'Enter your email address.', mobile: 'Enter your mobile number.' }[name];
    }
    if (c.errors?.['email']) return 'Check the email address: it looks like something is missing.';
    if (c.errors?.['mobile']) return 'Enter a Pakistani mobile number, for example 0300 1234567.';
    return '';
  }

  protected passwordError(): string {
    const c = this.form.controls.password;
    return (this.submitted() || c.touched) && c.errors ? 'Choose a password that meets every rule below.' : '';
  }

  protected submit(): void {
    this.submitted.set(true);
    if (this.form.invalid || this.state().busy) return;
    const { firstName, lastName, email, mobile, password, newsletter } = this.form.getRawValue();
    this.store.dispatch(
      CustomerAuthActions.registerSubmitted({
        input: { firstName: firstName.trim(), lastName: lastName.trim(), email: email.trim(), mobile: mobile.trim(), password, keepSignedIn: false, subscribeNewsletter: newsletter },
      }),
    );
  }
}
