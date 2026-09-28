import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FieldErrors } from '../shared/field-errors';
import { ApiError } from '../core/api-error';
import { inputValue } from '../shared/dom-events';
import { LOOKUP_TYPES, LookupTypeConfig } from './lookup-types';
import { LookupDto, ReferenceApiService } from './reference-api.service';

type Mode = 'list' | 'create' | 'edit';

/**
 * SCRUM-174: one screen for all eleven REF lookups - a type selector, a searchable table, and
 * add/edit/retire forms for the ten staff-maintained ones. PO Statuses renders read-only (AC-1/AC-2).
 */
@Component({
  selector: 'app-reference-data-page',
  imports: [ReactiveFormsModule, FieldErrors],
  templateUrl: './reference-data-page.html',
  styleUrl: './reference-data-page.scss',
})
export class ReferenceDataPage {
  private readonly api = inject(ReferenceApiService);
  private readonly formBuilder = inject(FormBuilder);

  readonly lookupTypes = LOOKUP_TYPES;
  readonly selectedType = signal<LookupTypeConfig>(LOOKUP_TYPES[0]);
  readonly items = signal<LookupDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly includeInactive = signal(false);
  readonly mode = signal<Mode>('list');
  readonly editingId = signal<number | null>(null);
  readonly saving = signal(false);

  readonly searchText = signal('');
  readonly visibleItems = computed(() => {
    const search = this.searchText().trim().toLowerCase();
    if (!search) {
      return this.items();
    }
    return this.items().filter(
      (item) => item.code.toLowerCase().includes(search) || item.name.toLowerCase().includes(search),
    );
  });

  readonly form = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: [''],
    sortSeq: [1, [Validators.required, Validators.min(0)]],
    parentCategoryId: this.formBuilder.control<number | null>(null),
    defaultAdvancePercent: this.formBuilder.control<number | null>(null),
  });

  constructor() {
    this.loadItems();
  }

  selectType(type: LookupTypeConfig): void {
    if (type.key === this.selectedType().key) {
      return;
    }
    this.selectedType.set(type);
    this.mode.set('list');
    this.searchText.set('');
    this.loadItems();
  }

  onSearchInput(event: Event): void {
    this.searchText.set(inputValue(event));
  }

  toggleIncludeInactive(): void {
    this.includeInactive.set(!this.includeInactive());
    this.loadItems();
  }

  startCreate(): void {
    this.mode.set('create');
    this.editingId.set(null);
    this.form.reset({ code: '', name: '', description: '', sortSeq: 1, parentCategoryId: null, defaultAdvancePercent: null });
    this.form.controls.code.enable();
  }

  startEdit(item: LookupDto): void {
    this.mode.set('edit');
    this.editingId.set(item.id);
    this.form.reset({
      code: item.code,
      name: item.name,
      description: item.description ?? '',
      sortSeq: item.sortSeq,
      parentCategoryId: item.parentCategoryId,
      defaultAdvancePercent: item.defaultAdvancePercent,
    });
    this.form.controls.code.disable();
  }

  cancelEdit(): void {
    this.mode.set('list');
    this.error.set(null);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const routeSegment = this.selectedType().key;

    const request =
      this.mode() === 'create'
        ? this.api.create(routeSegment, value)
        : this.api.update(routeSegment, this.editingId()!, value);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.mode.set('list');
        this.loadItems();
      },
      error: (error: ApiError) => {
        this.saving.set(false);
        this.error.set(error);
      },
    });
  }

  retire(item: LookupDto): void {
    if (!confirm(`Retire "${item.name}"? It will no longer be selectable for new records.`)) {
      return;
    }

    this.api.retire(this.selectedType().key, item.id).subscribe({
      next: () => this.loadItems(),
      error: (error: ApiError) => this.error.set(error),
    });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  private loadItems(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.list(this.selectedType().key, this.includeInactive()).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.error.set(error);
        this.loading.set(false);
      },
    });
  }
}
