import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { SignInValue } from '@core/auth';
import type { ApiError } from '@core/http';

export type { SignInValue } from '@core/auth';

/** "Try again in 30 seconds" / "2 minutes": the wait the server asked for, rounded up so the person is never early. */
export function waitText(seconds: number): string {
  if (seconds < 60) {
    return `${seconds} ${seconds === 1 ? 'second' : 'seconds'}`;
  }

  const minutes = Math.ceil(seconds / 60);
  return `${minutes} ${minutes === 1 ? 'minute' : 'minutes'}`;
}

/**
 * The staff sign-in form (sessions AC-7, AC-13). One generic message whichever part was wrong, a wait time when sign-in is throttled,
 * the password field emptied as soon as it is sent, and focus moved to the error summary so a keyboard or screen-reader user hears it.
 * Nothing typed here is stored anywhere in the browser; the browser's own password manager is left to do its job.
 */
@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule],
  template: `
    <section class="card sign-in" aria-labelledby="si-heading">
      <h1 id="si-heading">Sign in</h1>

      @if (message(); as text) {
        <p id="si-error" #errorSummary class="banner banner--error" role="alert" tabindex="-1">{{ text }}</p>
      }
      @if (mfaRequired()) {
        <p class="banner banner--info" role="status">
          Two-step verification is required for this account, but it is not available yet. Ask an Owner to help you.
        </p>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="form-field">
          <label for="si-user">User name</label>
          <input id="si-user" type="text" formControlName="userName" autocomplete="username" autocapitalize="none" spellcheck="false" maxlength="128" />
        </div>
        <div class="form-field">
          <label for="si-password">Password</label>
          <input id="si-password" type="password" formControlName="password" autocomplete="current-password" maxlength="128" />
        </div>
        <div class="form-actions">
          <button type="submit" class="button button--primary" [disabled]="signingIn()">{{ signingIn() ? 'Signing in…' : 'Sign in' }}</button>
        </div>
      </form>
    </section>
  `,
  styles: ':host { display: block; max-width: 28rem; margin: var(--space-6) auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly signingIn = input(false);
  readonly error = input<ApiError | null>(null);
  readonly mfaRequired = input(false);

  readonly submitted = output<SignInValue>();

  private readonly missing = signal<string | null>(null);
  private readonly errorSummary = viewChild<ElementRef<HTMLElement>>('errorSummary');

  readonly form = this.formBuilder.nonNullable.group({
    userName: ['', [Validators.required, Validators.pattern(/\S/)]],
    password: ['', [Validators.required]],
  });

  /** The one message shown: what is missing from the form, else what the server said, without saying which part was wrong. */
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
      return error.retryAfterSeconds
        ? `Too many attempts. Try again in ${waitText(error.retryAfterSeconds)}.`
        : 'Too many attempts. Wait a little and try again.';
    }

    return error.status === 401 ? "That user name and password didn't work. Check them and try again." : error.message;
  });

  constructor() {
    afterRenderEffect(() => {
      this.message();
      this.errorSummary()?.nativeElement.focus();
    });

  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.missing.set('Enter your user name and password.');
      return;
    }

    this.missing.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({ userName: value.userName.trim(), password: value.password });
    // The password is emptied the moment it is sent, so it never sits in the page after an attempt (and a fast retype is never wiped).
    this.form.controls.password.reset('');
  }
}
