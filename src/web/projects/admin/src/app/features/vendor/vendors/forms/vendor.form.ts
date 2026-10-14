import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

export type VendorFormGroup = FormGroup<{
  name: FormControl<string>;
  contactName: FormControl<string>;
  contactPhone: FormControl<string>;
  contactEmail: FormControl<string>;
  cityId: FormControl<number | null>;
  paymentTermId: FormControl<number | null>;
  isActive: FormControl<boolean>;
}>;

export function buildVendorForm(formBuilder: FormBuilder): VendorFormGroup {
  return formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    contactName: ['', [Validators.required, Validators.maxLength(100)]],
    contactPhone: ['', [Validators.required, Validators.maxLength(20)]],
    contactEmail: [''],
    cityId: formBuilder.control<number | null>(null, Validators.required),
    paymentTermId: formBuilder.control<number | null>(null, Validators.required),
    isActive: [true],
  });
}
