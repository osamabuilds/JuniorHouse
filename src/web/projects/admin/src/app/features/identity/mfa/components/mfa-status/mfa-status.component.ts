import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import type { ApiError } from '@core/http';
import type { MfaStatusDto } from '../../models';
import { digitsOnly, isSixDigits } from '../../code.util';

/** Below this many codes the person is told to get new ones. */
const LOW_RECOVERY_CODES = 3;

/**
 * The account's two-step status (mfa AC-6): on or off, how many recovery codes remain, and a way to get a new set. New codes need a
 * current authenticator code, so a borrowed session alone cannot make them, and the old ones stop working.
 */
@Component({
  selector: 'app-mfa-status',
  imports: [ReactiveFormsModule],
  template: `
    <section class="card mfa-status" aria-labelledby="ms-heading">
      <h1 id="ms-heading">Security</h1>

      @if (status().enrolled) {
        <p>Two-step verification is on.</p>
        <p>{{ remaining() }}</p>
        @if (low()) {
          <p class="banner banner--warning" role="status">You are running low on recovery codes. Get new ones and keep them somewhere safe.</p>
        }

        @if (error(); as failure) {
          <p class="banner banner--error" role="alert">{{ failure.message }}</p>
        }
        @if (summary(); as text) {
          <p class="banner banner--error" role="alert">{{ text }}</p>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <h2>Recovery codes</h2>
          <p class="field-hint">The old codes stop working as soon as you get new ones. To make new codes, enter the code your authenticator app shows now.</p>
          <div class="form-field">
            <label for="ms-code">Current code from your authenticator app</label>
            <input id="ms-code" type="text" formControlName="code" autocomplete="one-time-code" inputmode="numeric" maxlength="7" spellcheck="false" />
          </div>
          <div class="form-actions">
            <button type="submit" class="button" [disabled]="saving()">{{ saving() ? 'Working…' : 'Get new recovery codes' }}</button>
          </div>
        </form>
      } @else {
        <p>Two-step verification is not set up for this account.</p>
      }
    </section>
  `,
  styles: ':host { display: block; max-width: 36rem; margin: var(--space-6) auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MfaStatusComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly status = input.required<MfaStatusDto>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);

  readonly regenerateRequested = output<string>();

  readonly summary = signal<string | null>(null);
  readonly form = this.formBuilder.nonNullable.group({ code: [''] });

  readonly remaining = computed(() => {
    const count = this.status().recoveryCodesRemaining;
    return `${count} recovery ${count === 1 ? 'code' : 'codes'} left.`;
  });
  readonly low = computed(() => this.status().recoveryCodesRemaining <= LOW_RECOVERY_CODES);

  submit(): void {
    const typed = this.form.getRawValue().code;
    if (!isSixDigits(typed)) {
      this.summary.set('Enter the 6-digit code from your authenticator app.');
      return;
    }

    this.summary.set(null);
    this.regenerateRequested.emit(digitsOnly(typed));
    this.form.reset({ code: '' });
  }
}
