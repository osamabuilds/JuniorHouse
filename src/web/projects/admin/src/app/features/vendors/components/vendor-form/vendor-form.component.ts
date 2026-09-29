import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupDto } from '@features/reference-data';
import { ControlErrorsComponent, FieldErrorsComponent, inputChecked, PageMode } from '@shared';
import { VendorFormGroup } from '../../forms/vendor.form';

export interface SpecialisationToggle {
  readonly id: number;
  readonly checked: boolean;
}

@Component({
  selector: 'app-vendor-form',
  imports: [ReactiveFormsModule, FieldErrorsComponent, ControlErrorsComponent],
  templateUrl: './vendor-form.component.html',
  styleUrl: './vendor-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VendorFormComponent {
  readonly form = input.required<VendorFormGroup>();
  readonly mode = input.required<PageMode>();
  readonly saving = input.required<boolean>();
  readonly error = input.required<ApiError | null>();
  readonly cities = input.required<readonly LookupDto[]>();
  readonly paymentTerms = input.required<readonly LookupDto[]>();
  readonly specialisations = input.required<readonly LookupDto[]>();
  readonly selectedSpecialisationIds = input.required<readonly number[]>();

  readonly submitted = output<void>();
  readonly cancelled = output<void>();
  readonly specialisationToggled = output<SpecialisationToggle>();

  private readonly fieldErrorsByName = computed(() => this.error()?.fieldErrors ?? {});

  fieldErrors(field: string): readonly string[] {
    return this.fieldErrorsByName()[field] ?? [];
  }

  onSpecialisationToggle(id: number, event: Event): void {
    this.specialisationToggled.emit({ id, checked: inputChecked(event) });
  }
}
