import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { ActingPersonService, PaginationComponent, SectionNavComponent, inputValue, selectValue } from '@shared';
import { GtinHistoryComponent } from '../../components/gtin-history/gtin-history.component';
import { VariantTableComponent } from '../../components/variant-table/variant-table.component';
import { VoidFormComponent } from '../../components/void-form/void-form.component';
import { VariantRowDto, VariantStatusFilter, VoidGtinValue } from '../../models';
import { LABELLING_LINKS } from '../../variants.constants';
import {
  VariantsPageActions,
  selectCapacity,
  selectDetail,
  selectError,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectSearchText,
  selectStatus,
  selectTotal,
  selectVariants,
  selectVoidError,
  selectVoidSaving,
  selectVoidTarget,
} from '../../store';

/** The SKU register (docs/specs/labelling/variants/spec.md): every variant with its SKU and barcode, the capacity banner, history and void (VAR-8, VAR-12, VAR-13). */
@Component({
  selector: 'app-sku-register-page',
  imports: [SectionNavComponent, VariantTableComponent, GtinHistoryComponent, VoidFormComponent, PaginationComponent],
  templateUrl: './sku-register-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkuRegisterPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = LABELLING_LINKS;
  readonly variants = this.store.selectSignal(selectVariants);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly searchText = this.store.selectSignal(selectSearchText);
  readonly status = this.store.selectSignal(selectStatus);
  readonly capacity = this.store.selectSignal(selectCapacity);
  readonly detail = this.store.selectSignal(selectDetail);
  readonly voidTarget = this.store.selectSignal(selectVoidTarget);
  readonly voidSaving = this.store.selectSignal(selectVoidSaving);
  readonly voidError = this.store.selectSignal(selectVoidError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly defaultPerson = this.actingPerson.name;

  constructor() {
    this.store.dispatch(VariantsPageActions.opened());
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(VariantsPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  onStatusChange(event: Event): void {
    this.store.dispatch(VariantsPageActions.statusChanged({ status: selectValue(event) as VariantStatusFilter }));
  }

  refresh(): void {
    this.store.dispatch(VariantsPageActions.refreshRequested());
  }

  showDetails(variant: VariantRowDto): void {
    this.store.dispatch(VariantsPageActions.detailRequested({ id: variant.id }));
  }

  closeDetails(): void {
    this.store.dispatch(VariantsPageActions.detailClosed());
  }

  openVoidForm(variant: VariantRowDto): void {
    this.store.dispatch(VariantsPageActions.voidFormOpened({ variant }));
  }

  closeVoidForm(): void {
    this.store.dispatch(VariantsPageActions.voidFormClosed());
  }

  voidGtin(variant: VariantRowDto, value: VoidGtinValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(VariantsPageActions.voidSubmitted({ id: variant.id, value }));
  }

  dismissNotice(): void {
    this.store.dispatch(VariantsPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(VariantsPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(VariantsPageActions.pageSizeChanged({ pageSize: size }));
  }
}
