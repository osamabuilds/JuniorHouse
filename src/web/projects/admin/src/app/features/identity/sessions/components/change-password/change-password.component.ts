import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import type { ChangePasswordValue } from '@core/auth';
import type { ApiError } from '@core/http';

export type { ChangePasswordValue } from '@core/auth';

/** The shortest password the server accepts (staff-accounts AC-5); the server still decides, this only saves a round trip. */
export const MIN_PASSWORD_LENGTH = 12;

/**
 * Choosing your own password: required after a temporary one, and available to everyone. Three labelled fields, the rule stated up
 * front, the checks the browser can make made before asking the server, and focus moved to the summary on any problem. Nothing typed
 * here is kept: the fields are emptied after a rejection and the values live only in the request.
 */
@Component({
  selector: 'app-change-password',
  imports: [ReactiveFormsModule],
  template: `
    <section class="card change-password" aria-labelledby="cp-heading">
      <h1 id="cp-heading">Choose a new password</h1>
      @if (forced()) {
        <p class="banner banner--info" role="status">You need to choose your own password before you can continue.</p>
      }
      <p class="field-hint">Use at least {{ minLength }} characters. A short phrase of several words is easier to remember and harder to guess.</p>

      @if (problems().length > 0 || serverMessage()) {
        <div id="cp-error" #errorSummary class="banner banner--error" role="alert" tabindex="-1">
          @if (serverMessage(); as text) {
            <p>{{ text }}</p>
          }
          @if (problems().length > 0) {
            <p>This can't be changed yet.</p>
            <ul>
              @for (problem of problems(); track problem) {
                <li>{{ problem }}</li>
              }
            </ul>
          }
        </div>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="form-field">
          <label for="cp-current">Current password</label>
          <input id="cp-current" type="password" formControlName="currentPassword" autocomplete="current-password" maxlength="128" />
        </div>
        <div class="form-field">
          <label for="cp-new">New password</label>
          <input id="cp-new" type="password" formControlName="newPassword" autocomplete="new-password" maxlength="128" />
        </div>
        <div class="form-field">
          <label for="cp-confirm">Type the new password again</label>
          <input id="cp-confirm" type="password" formControlName="confirm" autocomplete="new-password" maxlength="128" />
        </div>
        <div class="form-actions">
          <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Change password' }}</button>
        </div>
      </form>
    </section>
  `,
  styles: ':host { display: block; max-width: 32rem; margin: var(--space-6) auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangePasswordComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly minLength = MIN_PASSWORD_LENGTH;

  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly forced = input(false);

  readonly submitted = output<ChangePasswordValue>();

  readonly problems = signal<readonly string[]>([]);
  readonly serverMessage = computed(() => this.error()?.message ?? null);
  private readonly errorSummary = viewChild<ElementRef<HTMLElement>>('errorSummary');

  readonly form = this.formBuilder.nonNullable.group({
    currentPassword: [''],
    newPassword: [''],
    confirm: [''],
  });

  constructor() {
    afterRenderEffect(() => {
      this.problems();
      this.serverMessage();
      this.errorSummary()?.nativeElement.focus();
    });

    effect(() => {
      if (this.error()) {
        this.form.reset({ currentPassword: '', newPassword: '', confirm: '' });
      }
    });
  }

  submit(): void {
    const { currentPassword, newPassword, confirm } = this.form.getRawValue();
    const found: string[] = [];
    if (!currentPassword) {
      found.push('Enter your current password.');
    }
    if (newPassword.length < MIN_PASSWORD_LENGTH) {
      found.push(`Choose a new password of at least ${MIN_PASSWORD_LENGTH} characters.`);
    } else if (newPassword !== confirm) {
      found.push("The new password and its confirmation don't match.");
    } else if (newPassword === currentPassword) {
      found.push('The new password must be different from the current one.');
    }

    this.problems.set(found);
    if (found.length === 0) {
      this.submitted.emit({ currentPassword, newPassword });
    }
  }
}
