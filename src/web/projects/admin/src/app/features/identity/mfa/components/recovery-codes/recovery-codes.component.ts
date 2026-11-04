import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';

/**
 * The ten recovery codes (mfa AC-6). Each works once, in place of an authenticator code, if the phone is lost. They are shown here and
 * never again, so the person copies or downloads them and must say they are saved before going on. Nothing is stored in the browser;
 * the store forgets the codes when this is dismissed.
 */
@Component({
  selector: 'app-recovery-codes',
  template: `
    <section class="card recovery-codes" aria-labelledby="rc-heading">
      <h1 id="rc-heading">Save your recovery codes</h1>
      <p class="banner banner--warning">
        This is the only time these codes are shown. If you lose your phone, each code lets you sign in once. Keep them somewhere safe, away
        from this computer's browser, such as a password manager or a printed sheet.
      </p>

      <ol class="recovery-codes__list">
        @for (code of codes(); track code) {
          <li>{{ code }}</li>
        }
      </ol>

      <div class="form-actions">
        <button type="button" class="button" (click)="copy()">Copy codes</button>
        <button type="button" class="button" (click)="download()">Download as a text file</button>
      </div>
      <p role="status" class="field-hint">{{ copied() ? 'Copied. Paste them somewhere safe now.' : '' }}</p>

      <div class="form-field">
        <label class="toolbar__checkbox">
          <input type="checkbox" [checked]="confirmed()" (change)="confirmed.set($any($event.target).checked)" />
          I have saved these codes somewhere safe
        </label>
      </div>
      <div class="form-actions">
        <button type="button" class="button button--primary" [disabled]="!confirmed()" (click)="saved.emit()">Continue</button>
      </div>
    </section>
  `,
  styles: ':host { display: block; max-width: 36rem; margin: var(--space-6) auto; } .recovery-codes__list { columns: 2; font-family: var(--font-mono, monospace); font-size: 1.125rem; letter-spacing: 0.05em; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecoveryCodesComponent {
  readonly codes = input.required<readonly string[]>();

  readonly saved = output<void>();

  readonly confirmed = signal(false);
  readonly copied = signal(false);

  async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.codes().join('\n'));
      this.copied.set(true);
    } catch {
      this.copied.set(false); // clipboard blocked: the list on screen and the download still work
    }
  }

  download(): void {
    const blob = new Blob([`Romp Admin recovery codes\n\n${this.codes().join('\n')}\n`], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'romp-recovery-codes.txt';
    link.click();
    URL.revokeObjectURL(url);
  }
}
