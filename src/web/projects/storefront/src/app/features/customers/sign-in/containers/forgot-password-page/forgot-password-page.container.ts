import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { CustomerAuthActions } from '../../../../../core/customer-auth';
import { selectResetRequest } from '../../../../../core/customer-auth/store/customer-auth.selectors';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { AuthLayoutComponent } from '../../components/auth-layout/auth-layout.component';
import { setUpAuthPage } from '../../utils/auth-page.util';

/**
 * Ask for a password-reset link (ACT-6). The answer is the same whether or not the address has an account, so the page never reveals which
 * addresses are registered.
 */
@Component({
  selector: 'rp-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, FieldComponent, IconComponent, AuthLayoutComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forgot-password-page.container.html',
})
export class ForgotPasswordPageContainer {
  private readonly store = inject(Store);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  constructor() {
    setUpAuthPage({ title: 'Reset your password', description: 'Ask for a link to set a new Romp password.', path: '/account/forgot-password' });
    this.store.dispatch(CustomerAuthActions.formOpened());
    // The form is replaced by the confirmation: move focus to it so it is announced.
    effect(() => {
      if (this.state().sent) queueMicrotask(() => this.heading()?.nativeElement.focus());
    });
  }

  protected readonly state = this.store.selectSignal(selectResetRequest);
  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });
  protected submitted = false;

  protected emailError(): string {
    const c = this.form.controls.email;
    if (!this.submitted && !c.touched) return '';
    return c.errors?.['required'] ? 'Enter your email address.' : c.errors?.['email'] ? 'Check the email address: it looks like something is missing.' : '';
  }

  protected again(): void {
    this.submitted = false;
    this.store.dispatch(CustomerAuthActions.formOpened());
  }

  protected submit(): void {
    this.submitted = true;
    if (this.form.invalid || this.state().busy) return;
    this.store.dispatch(CustomerAuthActions.resetRequested({ email: this.form.controls.email.value.trim() }));
  }
}
