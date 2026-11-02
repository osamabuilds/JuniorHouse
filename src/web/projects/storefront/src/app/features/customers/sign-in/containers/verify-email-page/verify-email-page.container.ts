import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, effect, inject, viewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { CustomerAuthActions } from '../../../../../core/customer-auth';
import { selectAuthStatus, selectResendStatus, selectVerifyStatus } from '../../../../../core/customer-auth/store/customer-auth.selectors';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { AuthLayoutComponent } from '../../components/auth-layout/auth-layout.component';
import { setUpAuthPage } from '../../utils/auth-page.util';

/**
 * The page the emailed verification link opens (ACT-27). The link is used from the browser, never during the server render, because a link
 * works once and the server render plus the browser would be two uses. Nothing is blocked either way: a link that does not work offers a new one.
 */
@Component({
  selector: 'rp-verify-email-page',
  imports: [RouterLink, ButtonDirective, AuthLayoutComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './verify-email-page.container.html',
})
export class VerifyEmailPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';

  protected readonly status = this.store.selectSignal(selectVerifyStatus);
  protected readonly signedIn = this.store.selectSignal(selectAuthStatus);
  protected readonly resend = this.store.selectSignal(selectResendStatus);
  protected readonly hasToken = this.token.length > 0;

  constructor() {
    setUpAuthPage({ title: 'Verify your email', description: 'Confirm your email address for your Romp account.', path: '/account/verify-email' });
    this.store.dispatch(CustomerAuthActions.formOpened());
    afterNextRender(() => {
      if (!this.hasToken) return;
      void this.router.navigate([], { queryParams: { token: null }, queryParamsHandling: 'merge', replaceUrl: true });
      this.store.dispatch(CustomerAuthActions.verifyEmailSubmitted({ token: this.token }));
    });
    effect(() => {
      if (this.status() !== 'idle' && this.status() !== 'busy') queueMicrotask(() => this.heading()?.nativeElement.focus());
    });
  }

  protected sendAnother(): void {
    this.store.dispatch(CustomerAuthActions.resendVerificationSubmitted());
  }
}
