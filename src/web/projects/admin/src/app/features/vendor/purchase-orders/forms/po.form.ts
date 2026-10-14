import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

export type PoFormGroup = FormGroup<{
  vendorId: FormControl<number | null>;
  styleId: FormControl<number | null>;
  unitCost: FormControl<number>;
  expectedDeliveryDate: FormControl<string>;
  paymentTermId: FormControl<number | null>;
  advancePercent: FormControl<number | null>;
  latestAcceptableDate: FormControl<string>;
  overTolerancePercent: FormControl<number | null>;
  underTolerancePercent: FormControl<number | null>;
  fabricResponsibilityId: FormControl<number | null>;
}>;

export function buildPoForm(formBuilder: FormBuilder): PoFormGroup {
  return formBuilder.nonNullable.group({
    vendorId: formBuilder.control<number | null>(null, Validators.required),
    styleId: formBuilder.control<number | null>(null, Validators.required),
    unitCost: [0, [Validators.required, Validators.min(0.01)]],
    expectedDeliveryDate: ['', Validators.required],
    paymentTermId: formBuilder.control<number | null>(null),
    advancePercent: formBuilder.control<number | null>(null),
    latestAcceptableDate: [''],
    overTolerancePercent: formBuilder.control<number | null>(null),
    underTolerancePercent: formBuilder.control<number | null>(null),
    fabricResponsibilityId: formBuilder.control<number | null>(null),
  });
}
