import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '../core/api-error';
import { LookupDto, ReferenceApiService } from '../reference-data/reference-api.service';
import { FieldErrors } from '../shared/field-errors';
import { CatalogApiService, StyleDto, StyleSummaryDto } from './catalog-api.service';

/** One editable cell of the colour x size target-quantity grid (AC-3). */
interface GridCell {
  readonly sizeId: number;
  readonly colourId: number;
  qty: number;
}

type Mode = 'list' | 'create' | 'edit';

/** SCRUM-174: style list (search/filter) plus a create/edit form with the colour x size target-quantity grid. */
@Component({
  selector: 'app-styles-page',
  imports: [ReactiveFormsModule, FieldErrors],
  templateUrl: './styles-page.html',
  styleUrl: './styles-page.scss',
})
export class StylesPage {
  private readonly catalogApi = inject(CatalogApiService);
  private readonly referenceApi = inject(ReferenceApiService);
  private readonly formBuilder = inject(FormBuilder);

  readonly styles = signal<StyleSummaryDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly mode = signal<Mode>('list');
  readonly editingId = signal<number | null>(null);
  readonly saving = signal(false);

  readonly searchText = signal('');
  readonly categoryFilter = signal<number | null>(null);
  readonly activeOnly = signal(true);

  readonly categories = signal<LookupDto[]>([]);
  readonly genders = signal<LookupDto[]>([]);
  readonly ageBrackets = signal<LookupDto[]>([]);
  readonly fabrics = signal<LookupDto[]>([]);
  readonly colours = signal<LookupDto[]>([]);
  readonly sizes = signal<LookupDto[]>([]);

  readonly selectedColourIds = signal<readonly number[]>([]);
  readonly selectedSizeIds = signal<readonly number[]>([]);
  private readonly gridQtyById = signal<ReadonlyMap<string, number>>(new Map());

  readonly gridRows = computed(() => {
    const sizes = this.sizes().filter((size) => this.selectedSizeIds().includes(size.id));
    const colours = this.colours().filter((colour) => this.selectedColourIds().includes(colour.id));
    const qtyById = this.gridQtyById();

    return sizes.map((size) => ({
      size,
      cells: colours.map<GridCell>((colour) => ({
        sizeId: size.id,
        colourId: colour.id,
        qty: qtyById.get(gridKey(size.id, colour.id)) ?? 0,
      })),
    }));
  });

  readonly form = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    collectionName: [''],
    categoryId: this.formBuilder.control<number | null>(null, Validators.required),
    genderId: this.formBuilder.control<number | null>(null, Validators.required),
    ageBracketId: this.formBuilder.control<number | null>(null, Validators.required),
    fabricId: this.formBuilder.control<number | null>(null, Validators.required),
    targetUnitCost: [0, [Validators.required, Validators.min(0)]],
    targetRetailPrice: [0, [Validators.required, Validators.min(0)]],
  });

  constructor() {
    this.loadStyles();
    this.referenceApi.list('categories', false).subscribe((items) => this.categories.set(items));
    this.referenceApi.list('genders', false).subscribe((items) => this.genders.set(items));
    this.referenceApi.list('age-brackets', false).subscribe((items) => this.ageBrackets.set(items));
    this.referenceApi.list('fabrics', false).subscribe((items) => this.fabrics.set(items));
    this.referenceApi.list('colours', false).subscribe((items) => this.colours.set(items));
    this.referenceApi.list('sizes', false).subscribe((items) => this.sizes.set(items));
  }

  applyFilters(): void {
    this.loadStyles();
  }

  toggleColour(colourId: number, checked: boolean): void {
    this.selectedColourIds.set(
      checked ? [...this.selectedColourIds(), colourId] : this.selectedColourIds().filter((id) => id !== colourId),
    );
  }

  toggleSize(sizeId: number, checked: boolean): void {
    this.selectedSizeIds.set(
      checked ? [...this.selectedSizeIds(), sizeId] : this.selectedSizeIds().filter((id) => id !== sizeId),
    );
  }

  setQty(sizeId: number, colourId: number, qty: number): void {
    const next = new Map(this.gridQtyById());
    next.set(gridKey(sizeId, colourId), qty);
    this.gridQtyById.set(next);
  }

  startCreate(): void {
    this.mode.set('create');
    this.editingId.set(null);
    this.error.set(null);
    this.form.reset({
      code: '',
      name: '',
      collectionName: '',
      categoryId: null,
      genderId: null,
      ageBracketId: null,
      fabricId: null,
      targetUnitCost: 0,
      targetRetailPrice: 0,
    });
    this.form.controls.code.enable();
    this.selectedColourIds.set([]);
    this.selectedSizeIds.set([]);
    this.gridQtyById.set(new Map());
  }

  startEdit(summary: StyleSummaryDto): void {
    this.catalogApi.getById(summary.id).subscribe((style: StyleDto) => {
      this.mode.set('edit');
      this.editingId.set(style.id);
      this.error.set(null);
      this.form.reset({
        code: style.code,
        name: style.name,
        collectionName: style.collectionName ?? '',
        categoryId: style.categoryId,
        genderId: style.genderId,
        ageBracketId: style.ageBracketId,
        fabricId: style.fabricId,
        targetUnitCost: style.targetUnitCost,
        targetRetailPrice: style.targetRetailPrice,
      });
      this.form.controls.code.disable();
      this.selectedColourIds.set(style.colourIds);
      this.selectedSizeIds.set(style.sizeIds);
      this.gridQtyById.set(
        new Map(style.targetLines.map((line) => [gridKey(line.sizeId, line.colourId), line.targetQty])),
      );
    });
  }

  cancelEdit(): void {
    this.mode.set('list');
  }

  save(): void {
    if (this.form.invalid || this.selectedColourIds().length === 0 || this.selectedSizeIds().length === 0) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const raw = this.form.getRawValue();
    const targetLines = this.gridRows().flatMap((row) =>
      row.cells.filter((cell) => cell.qty > 0).map((cell) => ({ sizeId: cell.sizeId, colourId: cell.colourId, targetQty: cell.qty })),
    );

    const value = {
      code: raw.code,
      name: raw.name,
      collectionName: raw.collectionName || null,
      categoryId: raw.categoryId!,
      genderId: raw.genderId!,
      ageBracketId: raw.ageBracketId!,
      fabricId: raw.fabricId!,
      targetUnitCost: raw.targetUnitCost,
      targetRetailPrice: raw.targetRetailPrice,
      colourIds: this.selectedColourIds(),
      sizeIds: this.selectedSizeIds(),
      targetLines,
    };

    const request = this.mode() === 'create' ? this.catalogApi.create(value) : this.catalogApi.update(this.editingId()!, value);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.mode.set('list');
        this.loadStyles();
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

  categoryName(categoryId: number): string {
    return this.categories().find((category) => category.id === categoryId)?.name ?? `#${categoryId}`;
  }

  private loadStyles(): void {
    this.loading.set(true);
    this.error.set(null);

    this.catalogApi.search(this.searchText(), this.categoryFilter(), this.activeOnly()).subscribe({
      next: (styles) => {
        this.styles.set(styles);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.error.set(error);
        this.loading.set(false);
      },
    });
  }
}

function gridKey(sizeId: number, colourId: number): string {
  return `${sizeId}-${colourId}`;
}
