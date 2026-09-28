import { Component, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';

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
export class ControlErrors {
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

/** "Target unit cost (PKR)" -> "target unit cost". */
export function plainName(label: string): string {
  return label.replace(/\s*\(.*?\)/g, '').trim().toLowerCase();
}

/**
 * A single sentence naming every required field that is still empty or invalid, for the banner
 * shown when someone tries to save an incomplete form. `labels` maps a control name to the words
 * staff see on screen; `extra` adds non-control requirements such as "at least one colour".
 */
export function missingSummary(
  controls: Readonly<Record<string, AbstractControl | null>>,
  labels: Readonly<Record<string, string>>,
  extra: readonly string[] = [],
): string {
  const missing = [
    ...Object.entries(labels)
      .filter(([key]) => controls[key]?.invalid)
      .map(([, label]) => plainName(label)),
    ...extra,
  ];
  return missing.length > 0
    ? `This can't be saved yet. Still needed: ${missing.join(', ')}.`
    : "This can't be saved yet. Please check the fields marked in red.";
}
