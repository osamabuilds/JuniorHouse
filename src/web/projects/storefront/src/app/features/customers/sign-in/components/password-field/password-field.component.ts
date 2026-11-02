import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';

/** A password input with the design system's Show / Hide button. Presentational: the form control comes in from the container. */
@Component({
  selector: 'rp-password-field',
  imports: [ReactiveFormsModule, FieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <rp-field #f [label]="label()" [hint]="hint()" [error]="error()">
      <div class="rp-pw">
        <input
          class="rp-input"
          [id]="f.controlId"
          [type]="shown() ? 'text' : 'password'"
          [formControl]="control()"
          [attr.autocomplete]="autocomplete()"
          [attr.aria-describedby]="f.describedBy()"
          [attr.aria-invalid]="f.invalid()"
          [attr.aria-required]="required() ? 'true' : null"
          [attr.name]="name()"
          spellcheck="false"
          autocapitalize="none"
        />
        <button type="button" [attr.aria-pressed]="shown()" [attr.aria-controls]="f.controlId" (click)="shown.set(!shown())">{{ shown() ? 'Hide' : 'Show' }}</button>
      </div>
    </rp-field>
  `,
})
export class PasswordFieldComponent {
  readonly control = input.required<FormControl<string>>();
  readonly label = input('Password');
  readonly hint = input('');
  readonly error = input('');
  readonly name = input('password');
  readonly required = input(true);
  /** `current-password` to sign in, `new-password` to choose one. */
  readonly autocomplete = input<'current-password' | 'new-password'>('current-password');

  protected readonly shown = signal(false);
}
