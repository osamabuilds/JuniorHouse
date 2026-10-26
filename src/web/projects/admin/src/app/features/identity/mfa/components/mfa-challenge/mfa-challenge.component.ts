import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import type { ApiError } from '@core/http';
import { waitText } from '@shared';
import { digitsOnly, isSixDigits } from '../../code.util';

type ChallengeMode = 'totp' | 'recovery';

/**
 * The second step of sign-in (mfa AC-3, AC-5, AC-6): the 6-digit code from the authenticator app, or a recovery code. One generic
 * message for any wrong code (never saying whether it was reused or expired), the wait when throttled, the field emptied as soon as the
 * code is sent, and focus on the message when it fails.
 */
@Component({
  selector: 'app-mfa-challenge',
  imports: [ReactiveFormsModule],
  template: `
    <section class="card mfa-challenge" aria-labelledby="mc-heading">
      <h1 id="mc-heading">Two-step verification</h1>
      <p>{{ mode() === 'totp' ? 'Open your authenticator app and enter the 6-digit code it shows.' : 'Enter one of the recovery codes you saved. Each works once.' }}</p>

      @if (message(); as text) {
        <p id="mc-error" #errorSummary class="banner banner--error" role="alert" tabindex="-1">{{ text }}</p>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="form-field">
          <label for="mc-code">{{ mode() === 'totp' ? 'Authentication code' : 'Recovery code' }}</label>
          <input
            id="mc-code"
            type="text"
            formControlName="code"
            [attr.autocomplete]="mode() === 'totp' ? 'one-time-code' : 'off'"
            [attr.inputmode]="mode() === 'totp' ? 'numeric' : null"
            [attr.maxlength]="mode() === 'totp' ? 7 : 16"
            autocapitalize="none"
            spellcheck="false"
          />
        </div>
        <div class="form-actions">
          <button type="submit" class="button button--primary" [disabled]="checking()">{{ checking() ? 'Checking…' : 'Verify' }}</button>
        </div>
      </form>

      <p>
        <button type="button" class="button button--link" (click)="toggle()">
          {{ mode() === 'totp' ? 'Use a recovery code instead' : 'Use your authenticator app instead' }}
        </button>
      </p>
      <p>
        <button type="button" class="button button--link" (click)="cancelled.emit()">Use a different account</button>
      </p>
    </section>
  `,
  styles: ':host { display: block; max-width: 28rem; margin: var(--space-6) auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MfaChallengeComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly checking = input(false);
  readonly error = input<ApiError | null>(null);

  readonly submitted = output<string>();
  readonly cancelled = output<void>();

  readonly mode = signal<ChallengeMode>('totp');
  private readonly missing = signal<string | null>(null);
  private readonly errorSummary = viewChild<ElementRef<HTMLElement>>('errorSummary');

  readonly form = this.formBuilder.nonNullable.group({ code: [''] });

  /** The one message shown: what is missing, else the wait, else a single wording for any wrong code. */
  readonly message = computed(() => {
    const missing = this.missing();
    if (missing) {
      return missing;
    }

    const error = this.error();
    if (!error) {
      return null;
    }

    if (error.status === 429) {
      return error.retryAfterSeconds ? `Too many attempts. Try again in ${waitText(error.retryAfterSeconds)}.` : 'Too many attempts. Wait a little and try again.';
    }

    return error.status === 401 ? "That code didn't work. Check it and try again." : error.message;
  });

  constructor() {
    afterRenderEffect(() => {
      this.message();
      this.errorSummary()?.nativeElement.focus();
    });
  }

  toggle(): void {
    this.mode.update((mode) => (mode === 'totp' ? 'recovery' : 'totp'));
    this.missing.set(null);
    this.form.reset({ code: '' });
  }

  submit(): void {
    const typed = this.form.getRawValue().code.trim();
    const valid = this.mode() === 'totp' ? isSixDigits(typed) : typed.length > 0;
    if (!valid) {
      this.missing.set(this.mode() === 'totp' ? 'Enter the 6-digit code from your authenticator app.' : 'Enter one of your recovery codes.');
      return;
    }

    this.missing.set(null);
    this.submitted.emit(this.mode() === 'totp' ? digitsOnly(typed) : typed);
    this.form.reset({ code: '' }); // emptied the moment it is sent
  }
}
