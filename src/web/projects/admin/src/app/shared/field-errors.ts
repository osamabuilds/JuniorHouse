import { Component, input } from '@angular/core';

/**
 * Renders a form field's server-side errors (AC-15) right under the control they belong to, the
 * same place client-side validation errors show, so staff see one consistent error location
 * regardless of which side rejected the value.
 */
@Component({
  selector: 'app-field-errors',
  template: `
    @for (message of messages(); track message) {
      <p class="field-error" role="alert">{{ message }}</p>
    }
  `,
  styles: `
    .field-error {
      color: var(--color-danger);
      font-size: 0.8125rem;
      margin: var(--space-1) 0 0;
    }
  `,
})
export class FieldErrors {
  readonly messages = input<readonly string[]>([]);
}
