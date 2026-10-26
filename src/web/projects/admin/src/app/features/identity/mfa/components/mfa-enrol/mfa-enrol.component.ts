import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import type { ApiError } from '@core/http';
import { digitsOnly, isSixDigits } from '../../code.util';
import type { MfaEnrolment } from '../../models';
import { QrCodeComponent } from '../qr-code/qr-code.component';

/**
 * Setting up two-step verification (mfa AC-1, AC-2): scan the QR code (or type the key), then enter the code the app shows. The secret
 * is shown here and nowhere else, and lives only in the page's memory. The code field takes a phone's autofill and is emptied as soon
 * as the code is sent. Focus goes to the message when something goes wrong.
 */
@Component({
  selector: 'app-mfa-enrol',
  imports: [ReactiveFormsModule, QrCodeComponent],
  template: `
    <section class="card mfa-enrol" aria-labelledby="me-heading">
      <h1 id="me-heading">Set up two-step verification</h1>
      <p>Your account needs a second step at every sign-in. It takes a minute.</p>

      @if (message(); as text) {
        <p id="me-error" #errorSummary class="banner banner--error" role="alert" tabindex="-1">{{ text }}</p>
      }

      <ol class="mfa-enrol__steps">
        <li>Install an authenticator app on your phone, such as Google Authenticator, Microsoft Authenticator or Authy.</li>
        <li>
          In the app, add an account and scan this code.
          <app-qr-code [text]="enrolment().otpAuthUri" label="QR code for your authenticator app. If you cannot scan it, type the setup key below instead." />
          <p class="field-hint">If you cannot scan it, choose "enter a setup key" in the app and type this key:</p>
          <p><code class="mfa-enrol__key">{{ enrolment().secretText }}</code></p>
        </li>
        <li>Type the 6-digit code the app now shows, to check it works.</li>
      </ol>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="form-field">
          <label for="me-code">6-digit code</label>
          <input id="me-code" type="text" formControlName="code" autocomplete="one-time-code" inputmode="numeric" maxlength="7" spellcheck="false" />
        </div>
        <div class="form-actions">
          <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Checking…' : 'Turn on two-step verification' }}</button>
          <button type="button" class="button" (click)="restartRequested.emit()">Get a new code to scan</button>
        </div>
      </form>
    </section>
  `,
  styles: ':host { display: block; max-width: 40rem; margin: var(--space-6) auto; } .mfa-enrol__key { font-size: 1.125rem; letter-spacing: 0.05em; word-break: break-all; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MfaEnrolComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly enrolment = input.required<MfaEnrolment>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);

  readonly codeSubmitted = output<string>();
  readonly restartRequested = output<void>();

  private readonly missing = signal<string | null>(null);
  private readonly errorSummary = viewChild<ElementRef<HTMLElement>>('errorSummary');

  readonly form = this.formBuilder.nonNullable.group({ code: [''] });

  readonly message = computed(() => this.missing() ?? this.error()?.message ?? null);

  constructor() {
    afterRenderEffect(() => {
      this.message();
      this.errorSummary()?.nativeElement.focus();
    });
  }

  submit(): void {
    const typed = this.form.getRawValue().code;
    if (!isSixDigits(typed)) {
      this.missing.set('Enter the 6-digit code from your authenticator app.');
      return;
    }

    this.missing.set(null);
    this.codeSubmitted.emit(digitsOnly(typed));
    this.form.reset({ code: '' }); // emptied the moment it is sent, so a code never sits in the page
  }
}
