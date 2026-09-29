import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Store } from '@ngrx/store';
import { missingSummary, PAGE_SIZE_OPTIONS, PaginationComponent, paginate } from '@shared';
import { inputValue } from '@shared';
import { LookupFormComponent, LookupTableComponent, LookupTypeTabsComponent } from '../../components';
import { buildLookupForm } from '../../forms/lookup.form';
import { LookupDto, LookupTypeConfig } from '../../models';
import { LOOKUP_TYPES } from '../../reference-data.constants';
import {
  ReferenceDataPageActions,
  selectEditingId,
  selectIncludeInactive,
  selectReferenceDataError,
  selectReferenceDataLoading,
  selectReferenceDataMode,
  selectReferenceDataSaving,
  selectSearchText,
  selectSelectedType,
  selectVisibleItems,
} from '../../store';

/**
 * SCRUM-174: one screen for the staff-facing REF lookups - a type selector, a searchable table, and
 * add/edit/retire forms for the staff-maintained ones. PO Statuses renders read-only (AC-1/AC-2).
 */
@Component({
  selector: 'app-reference-data-page',
  imports: [LookupTypeTabsComponent, LookupTableComponent, LookupFormComponent, PaginationComponent],
  templateUrl: './reference-data-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReferenceDataPageContainer {
  private readonly store = inject(Store);
  private readonly formBuilder = inject(FormBuilder);

  readonly lookupTypes = LOOKUP_TYPES;
  readonly selectedType = this.store.selectSignal(selectSelectedType);
  readonly allItems = this.store.selectSignal(selectVisibleItems);

  readonly page = signal(1);
  readonly pageSize = signal(PAGE_SIZE_OPTIONS[0]);
  private readonly paged = computed(() => paginate(this.allItems(), this.page(), this.pageSize()));
  readonly visibleItems = computed(() => this.paged().items);
  readonly total = computed(() => this.allItems().length);
  readonly currentPage = computed(() => this.paged().page);
  readonly loading = this.store.selectSignal(selectReferenceDataLoading);
  readonly error = this.store.selectSignal(selectReferenceDataError);
  readonly includeInactive = this.store.selectSignal(selectIncludeInactive);
  readonly mode = this.store.selectSignal(selectReferenceDataMode);
  readonly editingId = this.store.selectSignal(selectEditingId);
  readonly saving = this.store.selectSignal(selectReferenceDataSaving);
  readonly searchText = this.store.selectSignal(selectSearchText);

  readonly form = buildLookupForm(this.formBuilder);

  constructor() {
    this.store.dispatch(ReferenceDataPageActions.opened());
  }

  selectType(type: LookupTypeConfig): void {
    this.page.set(1);
    if (type.key === this.selectedType().key) {
      return;
    }
    this.store.dispatch(ReferenceDataPageActions.typeSelected({ typeKey: type.key }));
  }

  onSearchInput(event: Event): void {
    this.page.set(1);
    this.store.dispatch(ReferenceDataPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  toggleIncludeInactive(): void {
    this.page.set(1);
    this.store.dispatch(ReferenceDataPageActions.includeInactiveToggled());
  }

  startCreate(): void {
    this.form.reset({ code: '', name: '', description: '', sortSeq: 1, parentCategoryId: null, defaultAdvancePercent: null });
    this.form.controls.code.enable();
    this.store.dispatch(ReferenceDataPageActions.createStarted());
  }

  startEdit(item: LookupDto): void {
    this.form.reset({
      code: item.code,
      name: item.name,
      description: item.description ?? '',
      sortSeq: item.sortSeq,
      parentCategoryId: item.parentCategoryId,
      defaultAdvancePercent: item.defaultAdvancePercent,
    });
    this.form.controls.code.disable();
    this.store.dispatch(ReferenceDataPageActions.editStarted({ id: item.id }));
  }

  cancelEdit(): void {
    this.store.dispatch(ReferenceDataPageActions.editCancelled());
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.store.dispatch(
        ReferenceDataPageActions.formInvalid({
          error: {
            status: 0,
            message: missingSummary(this.form.controls, { code: 'Code', name: 'Name', sortSeq: 'Sort order' }),
            fieldErrors: {},
          },
        }),
      );
      return;
    }

    const value = this.form.getRawValue();
    const typeKey = this.selectedType().key;
    const id = this.editingId();

    this.store.dispatch(
      this.mode() === 'create'
        ? ReferenceDataPageActions.createSubmitted({ typeKey, value })
        : ReferenceDataPageActions.updateSubmitted({ typeKey, id: id!, value }),
    );
  }

  retire(item: LookupDto): void {
    if (!confirm(`Retire "${item.name}"? It will no longer be selectable for new records.`)) {
      return;
    }
    this.store.dispatch(ReferenceDataPageActions.retireConfirmed({ typeKey: this.selectedType().key, id: item.id }));
  }

  changePage(page: number): void {
    this.page.set(page);
  }

  changePageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }
}
