import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '../core/api-error';
import { LookupDto, ReferenceApiService } from '../reference-data/reference-api.service';
import { FieldErrors } from '../shared/field-errors';
import { VendorApiService, VendorDto, VendorSummaryDto } from './vendor-api.service';

type Mode = 'list' | 'create' | 'edit';

/** SCRUM-174: vendor list (search by name/city/specialisation) plus a create/edit form (FR-SC-01, AC-5/AC-5a/AC-6). */
@Component({
  selector: 'app-vendors-page',
  imports: [ReactiveFormsModule, FieldErrors],
  templateUrl: './vendors-page.html',
  styleUrl: './vendors-page.scss',
})
export class VendorsPage {
  private readonly vendorApi = inject(VendorApiService);
  private readonly referenceApi = inject(ReferenceApiService);
  private readonly formBuilder = inject(FormBuilder);

  readonly vendors = signal<VendorSummaryDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly mode = signal<Mode>('list');
  readonly editingId = signal<number | null>(null);
  readonly saving = signal(false);

  readonly searchText = signal('');
  readonly specialisationFilter = signal<number | null>(null);
  readonly activeOnly = signal(true);

  readonly cities = signal<LookupDto[]>([]);
  readonly paymentTerms = signal<LookupDto[]>([]);
  readonly specialisations = signal<LookupDto[]>([]);

  readonly selectedSpecialisationIds = signal<readonly number[]>([]);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    contactName: ['', [Validators.required, Validators.maxLength(100)]],
    contactPhone: ['', [Validators.required, Validators.maxLength(20)]],
    contactEmail: [''],
    cityId: this.formBuilder.control<number | null>(null, Validators.required),
    paymentTermId: this.formBuilder.control<number | null>(null, Validators.required),
    isActive: [true],
  });

  constructor() {
    this.loadVendors();
    this.referenceApi.list('cities', false).subscribe((items) => this.cities.set(items));
    this.referenceApi.list('payment-terms', false).subscribe((items) => this.paymentTerms.set(items));
    this.referenceApi.list('vendor-specialisations', false).subscribe((items) => this.specialisations.set(items));
  }

  applyFilters(): void {
    this.loadVendors();
  }

  toggleSpecialisation(specialisationId: number, checked: boolean): void {
    this.selectedSpecialisationIds.set(
      checked
        ? [...this.selectedSpecialisationIds(), specialisationId]
        : this.selectedSpecialisationIds().filter((id) => id !== specialisationId),
    );
  }

  startCreate(): void {
    this.mode.set('create');
    this.editingId.set(null);
    this.error.set(null);
    this.form.reset({ name: '', contactName: '', contactPhone: '', contactEmail: '', cityId: null, paymentTermId: null, isActive: true });
    this.form.controls.name.enable();
    this.selectedSpecialisationIds.set([]);
  }

  startEdit(summary: VendorSummaryDto): void {
    this.vendorApi.getById(summary.id).subscribe((vendor: VendorDto) => {
      this.mode.set('edit');
      this.editingId.set(vendor.id);
      this.error.set(null);
      this.form.reset({
        name: vendor.name,
        contactName: vendor.contactName,
        contactPhone: vendor.contactPhone,
        contactEmail: vendor.contactEmail ?? '',
        cityId: vendor.cityId,
        paymentTermId: vendor.paymentTermId,
        isActive: vendor.isActive,
      });
      this.form.controls.name.disable();
      this.selectedSpecialisationIds.set(vendor.specialisationIds);
    });
  }

  cancelEdit(): void {
    this.mode.set('list');
  }

  save(): void {
    if (this.form.invalid || this.selectedSpecialisationIds().length === 0) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const raw = this.form.getRawValue();

    const request =
      this.mode() === 'create'
        ? this.vendorApi.create({
            name: raw.name,
            contactName: raw.contactName,
            contactPhone: raw.contactPhone,
            contactEmail: raw.contactEmail || null,
            cityId: raw.cityId!,
            paymentTermId: raw.paymentTermId!,
            specialisationIds: this.selectedSpecialisationIds(),
          })
        : this.vendorApi.update(this.editingId()!, {
            contactName: raw.contactName,
            contactPhone: raw.contactPhone,
            contactEmail: raw.contactEmail || null,
            cityId: raw.cityId!,
            paymentTermId: raw.paymentTermId!,
            specialisationIds: this.selectedSpecialisationIds(),
            isActive: raw.isActive,
          });

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.mode.set('list');
        this.loadVendors();
      },
      error: (error: ApiError) => {
        this.saving.set(false);
        this.error.set(error);
      },
    });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  cityName(cityId: number): string {
    return this.cities().find((city) => city.id === cityId)?.name ?? `#${cityId}`;
  }

  private loadVendors(): void {
    this.loading.set(true);
    this.error.set(null);

    this.vendorApi.search(this.searchText(), this.specialisationFilter(), this.activeOnly()).subscribe({
      next: (vendors) => {
        this.vendors.set(vendors);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.error.set(error);
        this.loading.set(false);
      },
    });
  }
}
