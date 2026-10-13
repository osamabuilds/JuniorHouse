import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

export type LookupFormGroup = FormGroup<{
  code: FormControl<string>;
  name: FormControl<string>;
  description: FormControl<string>;
  sortSeq: FormControl<number>;
  parentCategoryId: FormControl<number | null>;
  defaultAdvancePercent: FormControl<number | null>;
}>;

export function buildLookupForm(formBuilder: FormBuilder): LookupFormGroup {
  return formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: [''],
    sortSeq: [1, [Validators.required, Validators.min(0)]],
    parentCategoryId: formBuilder.control<number | null>(null),
    defaultAdvancePercent: formBuilder.control<number | null>(null),
  });
}
