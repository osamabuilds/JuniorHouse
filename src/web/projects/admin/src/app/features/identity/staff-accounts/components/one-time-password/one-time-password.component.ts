import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TemporaryPasswordDto } from '../../models';

/**
 * Shows a generated password once (AC-4). It is the only place the value is rendered; "Done" asks the page to drop it from memory,
 * and nothing else can bring it back (the server cannot repeat it, only make a new one). The copy button uses the clipboard and
 * says so when the browser refuses, so the person can still select the text by hand.
 */
@Component({
  selector: 'app-one-time-password',
  imports: [DatePipe],
  template: `
    <section class="card" aria-labelledby="otp-heading">
      <h2 id="otp-heading">Temporary password for {{ password().userName }}</h2>
      <p class="banner banner--warning" role="alert">
        This is the only time it is shown. Give it to them in person or over a channel you trust. It works for one sign-in and
        lapses on {{ password().expiresAt | date: 'medium' }}.
      </p>
      <p>
        <output id="otp-value" class="mono" for="otp-copy">{{ password().temporaryPassword }}</output>
      </p>
      <div class="form-actions">
        <button id="otp-copy" type="button" class="button" (click)="copy()">Copy password</button>
        <button type="button" class="button button--primary" (click)="dismissed.emit()">Done, I have saved it</button>
      </div>
      <p class="field-hint" role="status">{{ copyMessage() }}</p>
    </section>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OneTimePasswordComponent {
  readonly password = input.required<TemporaryPasswordDto>();

  readonly dismissed = output<void>();

  readonly copyMessage = signal('');

  async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.password().temporaryPassword);
      this.copyMessage.set('Copied to the clipboard.');
    } catch {
      this.copyMessage.set('Your browser would not copy it. Select the password above and copy it by hand.');
    }
  }
}
