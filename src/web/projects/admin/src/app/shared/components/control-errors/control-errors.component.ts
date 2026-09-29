import { Component, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { plainName } from '../../utils/form-errors.util';

/**
 * Says what is wrong with a form field, in plain words, right under the field - but only after the
 * person has touched it or tried to save, so an untouched form isn't covered in red. This is the
 * browser-side half of validation (the API still checks everything and its messages are shown by
 * `app-field-errors`), and it exists because a Save button that silently does nothing is the worst
 * kind of error message.
 */
@Component({
  selector: 'app-control-errors',
  template: `
    @if (message(); as text) {
      <p class="field-error" role="alert">{{ text }}</p>
    }
  `,
})
export class ControlErrorsComponent {
  readonly control = input.required<AbstractControl>();
  /** The field's label, e.g. "Target unit cost (PKR)". */
  readonly label = input.required<string>();
  /** A text box is "entered", a dropdown is "chosen". */
  readonly kind = input<'enter' | 'choose'>('enter');

  message(): string | null {
    const control = this.control();
    if (!control.invalid || !(control.touched || control.dirty)) {
      return null;
    }

    const name = plainName(this.label());
    const errors = control.errors ?? {};
    if (errors['required']) {
      return this.kind() === 'choose' ? `Choose the ${name}.` : `Enter the ${name}.`;
    }
    if (errors['min']) {
      const min = (errors['min'] as { min: number }).min;
      return min > 0 ? `The ${name} must be at least ${min}.` : `The ${name} cannot be negative.`;
    }
    if (errors['maxlength']) {
      return `The ${name} can be at most ${(errors['maxlength'] as { requiredLength: number }).requiredLength} characters.`;
    }
    return `Check the ${name}.`;
  }
}
