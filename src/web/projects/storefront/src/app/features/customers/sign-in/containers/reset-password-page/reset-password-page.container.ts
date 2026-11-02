import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { CustomerAuthActions } from '../../../../../core/customer-auth';
import { selectResetConfirm } from '../../../../../core/customer-auth/store/customer-auth.selectors';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { AuthLayoutComponent } from '../../components/auth-layout/auth-layout.component';
import { PasswordChecklistComponent } from '../../components/password-checklist/password-checklist.component';
import { PasswordFieldComponent } from '../../components/password-field/password-field.component';
import { setUpAuthPage } from '../../utils/auth-page.util';
import { isPasswordValid } from '../../utils/password-rules.util';
import { toSignal } from '@angular/core/rxjs-interop';

/**
 * Set a new password from the emailed link (ACT-7, ACT-8). The link works once and expires; a used or old link is shown as such with a way to
 * ask for a new one. The token is taken out of the address bar as soon as it is read, so it does not stay in history or in a Referer header.
 */
@Component({
  selector: 'rp-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, IconComponent, AuthLayoutComponent, PasswordFieldComponent, PasswordChecklistComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reset-password-page.container.html',
})
export class ResetPasswordPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';

  protected readonly state = this.store.selectSignal(selectResetConfirm);
  protected readonly hasToken = this.token.length > 0;
  protected readonly form = new FormGroup({
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, (c) => (isPasswordValid(c.value) ? null : { rules: true })] }),
    confirm: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  protected readonly typedPassword = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });
  protected submitted = false;

  constructor() {
    setUpAuthPage({ title: 'Set a new password', description: 'Choose a new Romp password.', path: '/account/reset-password' });
    this.store.dispatch(CustomerAuthActions.formOpened());
    if (this.hasToken) void this.router.navigate([], { queryParams: { token: null }, queryParamsHandling: 'merge', replaceUrl: true });
    effect(() => {
      if (this.state().status !== 'idle' && this.state().status !== 'busy') queueMicrotask(() => this.heading()?.nativeElement.focus());
    });
  }

  protected passwordError(): string {
    const c = this.form.controls.password;
    if (this.state().unmetRules.length > 0) return 'That password does not meet every rule below.';
    return (this.submitted || c.touched) && c.errors ? 'Choose a password that meets every rule below.' : '';
  }

  protected confirmError(): string {
    const { password, confirm } = this.form.controls;
    if (!this.submitted && !confirm.touched) return '';
    return confirm.value !== password.value ? 'The two passwords are not the same. Type the new password again.' : '';
  }

  protected submit(): void {
    this.submitted = true;
    if (this.form.invalid || this.confirmError() || this.state().status === 'busy') return;
    this.store.dispatch(CustomerAuthActions.resetConfirmed({ token: this.token, newPassword: this.form.controls.password.value }));
  }
}
