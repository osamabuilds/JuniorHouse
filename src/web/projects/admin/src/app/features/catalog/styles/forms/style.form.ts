import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

export type StyleFormGroup = FormGroup<{
  code: FormControl<string>;
  name: FormControl<string>;
  collectionName: FormControl<string>;
  categoryId: FormControl<number | null>;
  genderId: FormControl<number | null>;
  ageBracketId: FormControl<number | null>;
  fabricId: FormControl<number | null>;
  targetUnitCost: FormControl<number>;
  targetRetailPrice: FormControl<number>;
}>;

export function buildStyleForm(formBuilder: FormBuilder): StyleFormGroup {
  return formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    collectionName: [''],
    categoryId: formBuilder.control<number | null>(null, Validators.required),
    genderId: formBuilder.control<number | null>(null, Validators.required),
    ageBracketId: formBuilder.control<number | null>(null, Validators.required),
    fabricId: formBuilder.control<number | null>(null, Validators.required),
    targetUnitCost: [0, [Validators.required, Validators.min(0)]],
    targetRetailPrice: [0, [Validators.required, Validators.min(0)]],
  });
}
