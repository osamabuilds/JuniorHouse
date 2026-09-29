import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Store } from '@ngrx/store';
import { ReferenceLookupActions, selectLookup } from '@features/reference-data';
import { inputValue, missingSummary, selectNumberOrNull, PaginationComponent } from '@shared';
import { SpecialisationToggle, VendorFormComponent } from '../../components/vendor-form/vendor-form.component';
import { VendorTableComponent } from '../../components/vendor-table/vendor-table.component';
import { buildVendorForm } from '../../forms/vendor.form';
import { VendorDto, VendorSummaryDto } from '../../models';
import {
  selectActiveOnly,
  selectEditingVendor,
  selectSearchText,
  selectSpecialisationFilter,
  selectVendors,
  selectVendorsPage,
  selectVendorsPageSize,
  selectVendorsTotal,
  selectVendorsError,
  selectVendorsLoading,
  selectVendorsMode,
  selectVendorsSaving,
  VendorsPageActions,
} from '../../store';

/** SCRUM-174: vendor list (search by name/city/specialisation) plus a create/edit form (FR-SC-01, AC-5/AC-5a/AC-6). */
@Component({
  selector: 'app-vendors-page',
  imports: [VendorTableComponent, VendorFormComponent, PaginationComponent],
  templateUrl: './vendors-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VendorsPageContainer {
  private readonly store = inject(Store);
  private readonly formBuilder = inject(FormBuilder);

  readonly vendors = this.store.selectSignal(selectVendors);
  readonly total = this.store.selectSignal(selectVendorsTotal);
  readonly currentPage = this.store.selectSignal(selectVendorsPage);
  readonly pageSize = this.store.selectSignal(selectVendorsPageSize);
  readonly loading = this.store.selectSignal(selectVendorsLoading);
  readonly error = this.store.selectSignal(selectVendorsError);
  readonly mode = this.store.selectSignal(selectVendorsMode);
  readonly saving = this.store.selectSignal(selectVendorsSaving);
  readonly searchText = this.store.selectSignal(selectSearchText);
  readonly specialisationFilter = this.store.selectSignal(selectSpecialisationFilter);
  readonly activeOnly = this.store.selectSignal(selectActiveOnly);
  private readonly editingVendor = this.store.selectSignal(selectEditingVendor);

  readonly cities = this.store.selectSignal(selectLookup('cities'));
  readonly paymentTerms = this.store.selectSignal(selectLookup('payment-terms'));
  readonly specialisations = this.store.selectSignal(selectLookup('vendor-specialisations'));

  readonly selectedSpecialisationIds = signal<readonly number[]>([]);

  readonly form = buildVendorForm(this.formBuilder);

  constructor() {
    this.store.dispatch(VendorsPageActions.opened());
    for (const typeKey of ['cities', 'payment-terms', 'vendor-specialisations']) {
      this.store.dispatch(ReferenceLookupActions.requested({ typeKey, includeInactive: false }));
    }

    // The record to edit arrives asynchronously; fill the form once it has.
    effect(() => {
      const vendor = this.editingVendor();
      if (vendor) {
        untracked(() => this.populateForm(vendor));
      }
    });
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(VendorsPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  onSpecialisationFilterChange(event: Event): void {
    this.store.dispatch(VendorsPageActions.specialisationFilterChanged({ specialisationId: selectNumberOrNull(event) }));
  }

  toggleActiveOnly(): void {
    this.store.dispatch(VendorsPageActions.activeOnlyToggled());
  }

  toggleSpecialisation({ id, checked }: SpecialisationToggle): void {
    this.selectedSpecialisationIds.set(
      checked ? [...this.selectedSpecialisationIds(), id] : this.selectedSpecialisationIds().filter((existing) => existing !== id),
    );
  }

  startCreate(): void {
    this.form.reset({ name: '', contactName: '', contactPhone: '', contactEmail: '', cityId: null, paymentTermId: null, isActive: true });
    this.form.controls.name.enable();
    this.selectedSpecialisationIds.set([]);
    this.store.dispatch(VendorsPageActions.createStarted());
  }

  startEdit(summary: VendorSummaryDto): void {
    this.store.dispatch(VendorsPageActions.editRequested({ id: summary.id }));
  }

  cancelEdit(): void {
    this.store.dispatch(VendorsPageActions.editCancelled());
  }

  save(): void {
    if (this.form.invalid || this.selectedSpecialisationIds().length === 0) {
      this.form.markAllAsTouched();
      this.store.dispatch(
        VendorsPageActions.formInvalid({
          error: {
            status: 0,
            message: missingSummary(
              this.form.controls,
              { name: 'Name', contactName: 'Contact name', contactPhone: 'Contact phone', cityId: 'City', paymentTermId: 'Default payment term' },
              this.selectedSpecialisationIds().length === 0 ? ['at least one specialisation'] : [],
            ),
            fieldErrors: {},
          },
        }),
      );
      return;
    }

    const raw = this.form.getRawValue();
    const common = {
      contactName: raw.contactName,
      contactPhone: raw.contactPhone,
      contactEmail: raw.contactEmail || null,
      cityId: raw.cityId!,
      paymentTermId: raw.paymentTermId!,
      specialisationIds: this.selectedSpecialisationIds(),
    };

    if (this.mode() === 'create') {
      this.store.dispatch(VendorsPageActions.createSubmitted({ value: { name: raw.name, ...common } }));
    } else {
      const id = this.editingVendor()!.id;
      this.store.dispatch(VendorsPageActions.updateSubmitted({ id, value: { ...common, isActive: raw.isActive } }));
    }
  }

  private populateForm(vendor: VendorDto): void {
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
  }

  changePage(page: number): void {
    this.store.dispatch(VendorsPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(VendorsPageActions.pageSizeChanged({ pageSize: size }));
  }
}
