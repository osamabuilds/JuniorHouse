import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Store } from '@ngrx/store';
import { ReferenceLookupActions, selectLookup } from '@features/reference-data';
import { inputValue, missingSummary, selectNumberOrNull, PaginationComponent } from '@shared';
import { OptionToggle, StyleFormComponent } from '../../components/style-form/style-form.component';
import { StyleTableComponent } from '../../components/style-table/style-table.component';
import { buildStyleForm } from '../../forms/style.form';
import { GridQtyChange, GridRow, gridKey, StyleDto, StyleSummaryDto } from '../../models';
import {
  selectActiveOnly,
  selectCategoryFilter,
  selectEditingStyle,
  selectSearchText,
  selectStyles,
  selectStylesPage,
  selectStylesPageSize,
  selectStylesTotal,
  selectStylesError,
  selectStylesLoading,
  selectStylesMode,
  selectStylesSaving,
  StylesPageActions,
} from '../../store';

const LOOKUP_TYPES = ['categories', 'genders', 'age-brackets', 'fabrics', 'colours', 'sizes'] as const;

/** SCRUM-174: style list (search/filter) plus a create/edit form with the colour x size target-quantity grid. */
@Component({
  selector: 'app-styles-page',
  imports: [StyleTableComponent, StyleFormComponent, PaginationComponent],
  templateUrl: './styles-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StylesPageContainer {
  private readonly store = inject(Store);
  private readonly formBuilder = inject(FormBuilder);

  readonly styles = this.store.selectSignal(selectStyles);
  readonly total = this.store.selectSignal(selectStylesTotal);
  readonly currentPage = this.store.selectSignal(selectStylesPage);
  readonly pageSize = this.store.selectSignal(selectStylesPageSize);
  readonly loading = this.store.selectSignal(selectStylesLoading);
  readonly error = this.store.selectSignal(selectStylesError);
  readonly mode = this.store.selectSignal(selectStylesMode);
  readonly saving = this.store.selectSignal(selectStylesSaving);
  readonly searchText = this.store.selectSignal(selectSearchText);
  readonly categoryFilter = this.store.selectSignal(selectCategoryFilter);
  readonly activeOnly = this.store.selectSignal(selectActiveOnly);
  private readonly editingStyle = this.store.selectSignal(selectEditingStyle);

  readonly categories = this.store.selectSignal(selectLookup('categories'));
  readonly genders = this.store.selectSignal(selectLookup('genders'));
  readonly ageBrackets = this.store.selectSignal(selectLookup('age-brackets'));
  readonly fabrics = this.store.selectSignal(selectLookup('fabrics'));
  readonly colours = this.store.selectSignal(selectLookup('colours'));
  readonly sizes = this.store.selectSignal(selectLookup('sizes'));

  readonly selectedColourIds = signal<readonly number[]>([]);
  readonly selectedSizeIds = signal<readonly number[]>([]);
  private readonly gridQtyById = signal<ReadonlyMap<string, number>>(new Map());

  readonly gridRows = computed<GridRow[]>(() => {
    const sizes = this.sizes().filter((size) => this.selectedSizeIds().includes(size.id));
    const colours = this.colours().filter((colour) => this.selectedColourIds().includes(colour.id));
    const qtyById = this.gridQtyById();

    return sizes.map((size) => ({
      size,
      cells: colours.map((colour) => ({
        sizeId: size.id,
        colourId: colour.id,
        qty: qtyById.get(gridKey(size.id, colour.id)) ?? 0,
      })),
    }));
  });

  readonly form = buildStyleForm(this.formBuilder);

  constructor() {
    this.store.dispatch(StylesPageActions.opened());
    for (const typeKey of LOOKUP_TYPES) {
      this.store.dispatch(ReferenceLookupActions.requested({ typeKey, includeInactive: false }));
    }

    // The record to edit arrives asynchronously; fill the form once it has.
    effect(() => {
      const style = this.editingStyle();
      if (style) {
        untracked(() => this.populateForm(style));
      }
    });
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(StylesPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  onCategoryFilterChange(event: Event): void {
    this.store.dispatch(StylesPageActions.categoryFilterChanged({ categoryId: selectNumberOrNull(event) }));
  }

  toggleActiveOnly(): void {
    this.store.dispatch(StylesPageActions.activeOnlyToggled());
  }

  toggleColour({ id, checked }: OptionToggle): void {
    this.selectedColourIds.set(
      checked ? [...this.selectedColourIds(), id] : this.selectedColourIds().filter((existing) => existing !== id),
    );
  }

  toggleSize({ id, checked }: OptionToggle): void {
    this.selectedSizeIds.set(
      checked ? [...this.selectedSizeIds(), id] : this.selectedSizeIds().filter((existing) => existing !== id),
    );
  }

  setQty({ sizeId, colourId, qty }: GridQtyChange): void {
    const next = new Map(this.gridQtyById());
    next.set(gridKey(sizeId, colourId), qty);
    this.gridQtyById.set(next);
  }

  startCreate(): void {
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
    this.store.dispatch(StylesPageActions.createStarted());
  }

  startEdit(summary: StyleSummaryDto): void {
    this.store.dispatch(StylesPageActions.editRequested({ id: summary.id }));
  }

  cancelEdit(): void {
    this.store.dispatch(StylesPageActions.editCancelled());
  }

  save(): void {
    if (this.form.invalid || this.selectedColourIds().length === 0 || this.selectedSizeIds().length === 0) {
      this.form.markAllAsTouched();
      this.store.dispatch(
        StylesPageActions.formInvalid({
          error: {
            status: 0,
            message: missingSummary(
              this.form.controls,
              {
                code: 'Code',
                name: 'Name',
                categoryId: 'Category',
                genderId: 'Gender',
                ageBracketId: 'Age bracket',
                fabricId: 'Fabric',
                targetUnitCost: 'Target unit cost',
                targetRetailPrice: 'Target retail price',
              },
              [
                ...(this.selectedColourIds().length === 0 ? ['at least one colourway'] : []),
                ...(this.selectedSizeIds().length === 0 ? ['at least one size'] : []),
              ],
            ),
            fieldErrors: {},
          },
        }),
      );
      return;
    }

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

    this.store.dispatch(
      this.mode() === 'create'
        ? StylesPageActions.createSubmitted({ value })
        : StylesPageActions.updateSubmitted({ id: this.editingStyle()!.id, value }),
    );
  }

  private populateForm(style: StyleDto): void {
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
    this.gridQtyById.set(new Map(style.targetLines.map((line) => [gridKey(line.sizeId, line.colourId), line.targetQty])));
  }

  changePage(page: number): void {
    this.store.dispatch(StylesPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(StylesPageActions.pageSizeChanged({ pageSize: size }));
  }
}
