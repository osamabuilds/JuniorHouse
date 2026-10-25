import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

export type SynonymFormGroup = FormGroup<{
  name: FormControl<string>;
  isActive: FormControl<boolean>;
  /** One term per line (commas also separate terms). */
  terms: FormControl<string>;
}>;

export function buildSynonymForm(formBuilder: FormBuilder): SynonymFormGroup {
  return formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(60)]],
    isActive: [true],
    terms: ['', [Validators.required]],
  });
}

/** The terms typed in the box: split on new lines and commas, trimmed, empty ones dropped. The server checks the rest. */
export function parseTerms(text: string): string[] {
  return text
    .split(/[\r\n,]+/)
    .map((t) => t.trim())
    .filter((t) => t.length > 0);
}

export function termsToText(terms: readonly string[]): string {
  return terms.join('\n');
}
