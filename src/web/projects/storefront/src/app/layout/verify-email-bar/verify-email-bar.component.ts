import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { CustomerAuthActions, selectResendStatus, selectShowVerifyNotice } from '../../core/customer-auth';
import { ButtonDirective } from '../../shared/ui/button/button.directive';

/**
 * The unverified-email notice (ACT-31): shown on every page to a signed-in customer whose email is not yet confirmed, with a button that
 * sends a fresh link. It informs and never blocks: browsing, the address book and placing orders work as for anyone else.
 */
@Component({
  selector: 'rp-verify-email-bar',
  imports: [ButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (show()) {
      <aside class="rp-verify-bar" aria-label="Email verification">
        <div class="rp-wrap rp-verify-bar__row">
          <p role="status">
            @switch (resend()) {
              @case ('sent') { A new verification link is on its way. Open it from your email to finish. }
              @case ('tooMany') { You asked for several links recently. Please try again in a little while. }
              @case ('error') { We could not send the link just now. Please try again in a moment. }
              @default { Your email address is not verified yet. }
            }
          </p>
          @if (resend() !== 'sent' && resend() !== 'tooMany') {
            <button rpBtn="ghost" size="sm" type="button" [disabled]="resend() === 'busy'" (click)="send()">Verify my email</button>
          }
        </div>
      </aside>
    }
  `,
  styles: ':host { display: block; }',
})
export class VerifyEmailBarComponent {
  private readonly store = inject(Store);
  protected readonly show = this.store.selectSignal(selectShowVerifyNotice);
  protected readonly resend = this.store.selectSignal(selectResendStatus);

  protected send(): void {
    this.store.dispatch(CustomerAuthActions.resendVerificationSubmitted());
  }
}
